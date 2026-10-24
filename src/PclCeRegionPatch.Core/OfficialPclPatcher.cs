using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PclCeRegionPatch;

/// <summary>
///     Patcher for the official PCL (Plain Craft Launcher) 2.99.0.0 build.
///     <para>
///         Upstream implemented the download restriction with a compile-time constant
///         (<c>ModSecret.RestrictMcDownload = True</c>), so the restricted builds have the
///         alternative code paths removed by the compiler at several entry points. This patcher
///         rebuilds the two entry points needed to use the Minecraft download page again:
///         <c>PageDownloadInstall.LoaderInit</c> and <c>PageDownloadModify.LoadInstanceFinished</c>.
///     </para>
/// </summary>
internal static class OfficialPclPatcher
{
    internal const string ModSecretTypeName = "PCL.ModSecret";
    internal const string RestrictFieldName = "RestrictMcDownload";
    internal const string PageDownloadInstallTypeName = "PCL.PageDownloadInstall";
    internal const string PageDownloadModifyTypeName = "PCL.PageDownloadModify";
    internal const string ModDownloadTypeName = "PCL.ModDownload";
    internal const string LoaderInitMethodName = "LoaderInit";
    internal const string ModifyLoadMethodName = "LoadInstanceFinished";
    internal const string DlClientListLoaderFieldName = "DlClientListLoader";
    internal const string RestrictedLoaderText = "关闭下载";

    public static PatchInspection Inspect(byte[] data)
    {
        using var assembly = TryRead(data)
                             ?? throw new PatchNotSupportedException(
                                 "无法识别的文件：既不是 PCL CE 单文件构建，也不是 PCL 官方版 .NET 程序集。");

        ValidateStructure(assembly);
        var loaderInit = FindLoaderInit(assembly)
                         ?? throw new PatchNotSupportedException("找不到 PageDownloadInstall.LoaderInit。");
        var isPatched = IsLoaderInitPatched(loaderInit);

        var details = new List<string>
        {
            $"目标程序集：{assembly.Name.Name} {assembly.Name.Version}",
            $"运行时：{assembly.MainModule.Runtime}（.NET Framework）",
            "限制方式：编译期常量 ModSecret.RestrictMcDownload = True",
            "可恢复入口：下载页 Minecraft 版本列表、实例修改页的 Minecraft 卡片",
            "限制范围：全量禁用 MC 下载（不是按地区判断）",
            "说明：上游在发布版中删除了整合包安装等入口的代码，本工具暂不恢复该部分"
        };
        details.Add(isPatched ? "下载限制：已解除（MC 下载功能已恢复）" : "下载限制：未解除（MC 下载功能受限）");

        return new PatchInspection
        {
            Target = PatchTarget.PclOfficial,
            ProductName = "PCL 官方版（.NET Framework）",
            Version = assembly.Name.Version.ToString(),
            IsPatched = isPatched,
            Details = details
        };
    }

    public static PatchResult Apply(byte[] data)
    {
        var steps = new List<string>();
        var verification = new List<string>();
        var depsDirectory = Path.Combine(Path.GetTempPath(), "pcl-regionpatch-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(depsDirectory);
            ExtractEmbeddedAssemblies(data, depsDirectory, steps);

            using var resolver = CreateResolver(depsDirectory);
            using var input = new MemoryStream(data, writable: false);
            using var assembly = AssemblyDefinition.ReadAssembly(input, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadingMode = ReadingMode.Immediate
            });

            ValidateStructure(assembly);

            var loaderInit = FindLoaderInit(assembly)
                             ?? throw new PatchNotSupportedException("找不到 PageDownloadInstall.LoaderInit。");
            PatchLoaderInit(assembly.MainModule, loaderInit, steps);
            PatchModifyVisibility(assembly.MainModule, steps);

            using var output = new MemoryStream();
            assembly.Write(output);
            var patched = output.ToArray();

            Verify(patched, verification);
            return new PatchResult
            {
                Output = patched,
                Steps = steps,
                Verification = verification
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(depsDirectory)) Directory.Delete(depsDirectory, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    // ------------------------------------------------------------------ validation

    private static AssemblyDefinition? TryRead(byte[] data)
    {
        try
        {
            return AssemblyDefinition.ReadAssembly(new MemoryStream(data, writable: false));
        }
        catch
        {
            return null;
        }
    }

    private static void ValidateStructure(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;
        var secret = module.GetType(ModSecretTypeName);
        var restrict = secret?.Fields.FirstOrDefault(field => field.Name == RestrictFieldName);
        if (restrict is null || !restrict.HasConstant || restrict.Constant is not true)
            throw new PatchNotSupportedException(
                "该文件不是受支持的 PCL 官方版：没有找到 ModSecret.RestrictMcDownload = True 的编译期限制。");

        if (FindLoaderInit(assembly) is null)
            throw new PatchNotSupportedException(
                "该版本的结构与当前支持的 PCL 官方版（2.99.0.0）不一致：找不到 PageDownloadInstall.LoaderInit。");

        var modDownloadType = module.GetType(ModDownloadTypeName);
        if (modDownloadType is null ||
            modDownloadType.Fields.All(field => field.Name != DlClientListLoaderFieldName))
            throw new PatchNotSupportedException(
                "该版本的 ModDownload.DlClientListLoader 不存在，无法恢复下载页。");

        var modifyType = module.GetType(PageDownloadModifyTypeName);
        if (modifyType is null || modifyType.Methods.All(method => method.Name != ModifyLoadMethodName))
            throw new PatchNotSupportedException(
                "该版本的 PageDownloadModify.LoadInstanceFinished 不存在，无法恢复修改页。");
    }

    private static MethodDefinition? FindLoaderInit(AssemblyDefinition assembly) =>
        assembly.MainModule.GetType(PageDownloadInstallTypeName)?
            .Methods.FirstOrDefault(method => method.Name == LoaderInitMethodName && method.Parameters.Count == 0);

    private static bool IsLoaderInitPatched(MethodDefinition loaderInit)
    {
        var instructions = loaderInit.Body.Instructions;
        var usesRealLoader = instructions.Any(instruction =>
            instruction.OpCode == OpCodes.Ldsfld &&
            instruction.Operand is FieldReference field &&
            field.Name == DlClientListLoaderFieldName);
        var hasRestrictedText = instructions.Any(instruction =>
            instruction.OpCode == OpCodes.Ldstr &&
            instruction.Operand is string text &&
            text.Contains(RestrictedLoaderText, StringComparison.Ordinal));
        return usesRealLoader && !hasRestrictedText;
    }

    // ------------------------------------------------------------------ embedded dependencies

    private static void ExtractEmbeddedAssemblies(byte[] data, string directory, List<string> steps)
    {
        using var assembly = TryRead(data)
                             ?? throw new PatchNotSupportedException("无法读取启动器程序集。");

        var resource = assembly.MainModule.Resources
            .OfType<EmbeddedResource>()
            .FirstOrDefault(item => item.Name == "PCL.Resources.resources");
        if (resource is null)
            throw new PatchNotSupportedException(
                "该文件中没有找到 PCL.Resources 资源，无法解析其内嵌依赖程序集。");

        var count = 0;
        using (var stream = new MemoryStream(resource.GetResourceData()))
        using (var reader = new ResourceReader(stream))
        {
            foreach (DictionaryEntry entry in reader)
            {
                if (entry.Value is not byte[] bytes || bytes.Length < 1024) continue;
                if (bytes.Length < 2 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z') continue;

                var name = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) continue;
                if (name.Contains(Path.DirectorySeparatorChar) || name.Contains('/')) continue;

                File.WriteAllBytes(Path.Combine(directory, name + ".dll"), bytes);
                count++;
            }
        }

        if (count == 0)
            throw new PatchNotSupportedException("未能从启动器资源中提取内嵌依赖程序集。");

        steps.Add($"从 PCL.Resources 中提取 {count} 个内嵌依赖程序集用于元数据解析（仅临时目录，完成后删除）");
    }

    private static DefaultAssemblyResolver CreateResolver(string depsDirectory)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(depsDirectory);

        var netRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET");
        foreach (var architecture in new[] { "Framework64", "Framework" })
        {
            var frameworkDirectory = Path.Combine(netRoot, architecture, "v4.0.30319");
            if (!Directory.Exists(frameworkDirectory)) continue;
            resolver.AddSearchDirectory(frameworkDirectory);
            var wpfDirectory = Path.Combine(frameworkDirectory, "WPF");
            if (Directory.Exists(wpfDirectory)) resolver.AddSearchDirectory(wpfDirectory);
        }

        return resolver;
    }

    // ------------------------------------------------------------------ patches

    private static void PatchLoaderInit(ModuleDefinition module, MethodDefinition loaderInit, List<string> steps)
    {
        var oldInstructions = loaderInit.Body.Instructions.ToList();

        MethodReference FindCall(string name) =>
            oldInstructions.Select(instruction => instruction.Operand).OfType<MethodReference>()
                .FirstOrDefault(reference => reference.Name == name)
            ?? throw new PatchNotSupportedException($"找不到所需的成员引用：{name}");

        var controlInit = FindCall("ControlInit");
        var getLoadMinecraft = FindCall("get_LoadMinecraft");
        var getPanLoad = FindCall("get_PanLoad");
        var getPanAllBack = FindCall("get_PanAllBack");
        var pageLoaderInit = FindCall("PageLoaderInit");

        var actionCtor = oldInstructions.Select(instruction => instruction.Operand).OfType<MethodReference>()
            .FirstOrDefault(reference =>
                reference.Name == ".ctor" &&
                reference.DeclaringType.FullName.Contains("System.Action`1", StringComparison.Ordinal) &&
                reference.DeclaringType.FullName.Contains("LoaderTask", StringComparison.Ordinal))
            ?? throw new PatchNotSupportedException("找不到下载状态回调的委托构造引用。");

        var onFinishLambda = module.GetType(PageDownloadInstallTypeName)?
            .Methods.FirstOrDefault(method =>
                method.Name.StartsWith("_Lambda$__R3-", StringComparison.Ordinal) &&
                method.Parameters.Count == 1 &&
                method.Parameters[0].ParameterType.FullName.Contains("LoaderTask", StringComparison.Ordinal) &&
                method.HasBody &&
                method.Body.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is MethodReference target &&
                    target.Name == "LoadMinecraft_OnFinish"))
            ?? throw new PatchNotSupportedException("找不到下载列表加载完成回调（LoadMinecraft_OnFinish 的 lambda）。");

        var modDownload = module.GetType(ModDownloadTypeName)
                          ?? throw new PatchNotSupportedException("找不到 ModDownload 类型。");
        var dlLoaderField = modDownload.Fields.FirstOrDefault(field => field.Name == DlClientListLoaderFieldName)
                            ?? throw new PatchNotSupportedException("找不到 ModDownload.DlClientListLoader 字段。");

        var body = new MethodBody(loaderInit);
        var il = body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, controlInit);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, getLoadMinecraft);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, getPanLoad);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, getPanAllBack);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldsfld, dlLoaderField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldftn, onFinishLambda);
        il.Emit(OpCodes.Newobj, actionCtor);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Call, pageLoaderInit);
        il.Emit(OpCodes.Ret);
        body.MaxStackSize = 8;
        body.InitLocals = false;
        loaderInit.Body = body;

        steps.Add("重建 PageDownloadInstall.LoaderInit：改为加载真实的 Minecraft 版本列表（DlClientListLoader）");
    }

    private static void PatchModifyVisibility(ModuleDefinition module, List<string> steps)
    {
        var modify = module.GetType(PageDownloadModifyTypeName)
                     ?? throw new PatchNotSupportedException("找不到 PageDownloadModify 类型。");
        var method = modify.Methods.FirstOrDefault(candidate => candidate.Name == ModifyLoadMethodName)
                     ?? throw new PatchNotSupportedException("找不到 PageDownloadModify.LoadInstanceFinished。");

        var instructions = method.Body.Instructions.ToList();
        var il = method.Body.GetILProcessor();
        var patched = 0;
        var alreadyVisible = 0;
        for (var i = 1; i < instructions.Count - 1; i++)
        {
            if (instructions[i - 1].Operand is not MethodReference previous ||
                previous.Name != "get_CardMinecraft") continue;
            if (instructions[i + 1].Operand is not MethodReference next ||
                next.Name != "set_Visibility") continue;

            if (instructions[i].OpCode == OpCodes.Ldc_I4_2)
            {
                il.Replace(instructions[i], Instruction.Create(OpCodes.Ldc_I4_0));
                patched++;
            }
            else if (instructions[i].OpCode == OpCodes.Ldc_I4_0)
            {
                alreadyVisible++;
            }
        }

        if (patched == 0 && alreadyVisible == 0)
            throw new PatchNotSupportedException(
                "未能定位修改页的 Minecraft 卡片可见性设置，可能是未适配的版本。");

        if (patched > 0)
            steps.Add($"恢复 PageDownloadModify.LoadInstanceFinished 中 Minecraft 卡片的可见性（{patched} 处）");
        else
            steps.Add("PageDownloadModify 中 Minecraft 卡片已经是 Visible，无需再次修改");
    }

    // ------------------------------------------------------------------ verification

    private static void Verify(byte[] patched, List<string> verification)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(patched, writable: false));

        var loaderInit = FindLoaderInit(assembly)
                         ?? throw new InvalidOperationException("校验失败：补丁后找不到 LoaderInit。");
        if (!IsLoaderInitPatched(loaderInit))
            throw new InvalidOperationException("校验失败：LoaderInit 未包含真实的下载列表加载逻辑。");

        verification.Add("LoaderInit：已使用 DlClientListLoader 加载真实的 Minecraft 版本列表");
        verification.Add("LoaderInit 开头指令：" + string.Join(
            "; ",
            loaderInit.Body.Instructions.Take(6).Select(instruction => instruction.OpCode.Name)));

        var modify = assembly.MainModule.GetType(PageDownloadModifyTypeName)!
            .Methods.First(method => method.Name == ModifyLoadMethodName);
        var instructions = modify.Body.Instructions.ToList();
        for (var i = 1; i < instructions.Count - 1; i++)
        {
            if (instructions[i - 1].Operand is MethodReference previous &&
                previous.Name == "get_CardMinecraft" &&
                instructions[i + 1].Operand is MethodReference next &&
                next.Name == "set_Visibility")
            {
                if (instructions[i].OpCode != OpCodes.Ldc_I4_0)
                    throw new InvalidOperationException("校验失败：Minecraft 卡片可见性未恢复。");
                verification.Add("LoadInstanceFinished：Minecraft 卡片可见性 = Visible");
            }
        }
    }
}
