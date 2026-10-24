using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PclCeRegionPatch.Gui;

internal sealed class MainForm : Form
{
    private const string AppTitle = "PCL CE 区域限制补丁工具";
    private const string AppVersion = "1.1.0";

    private static readonly Color Accent = Color.FromArgb(0, 102, 184);
    private static readonly Color OkColor = Color.FromArgb(16, 124, 16);
    private static readonly Color WarnColor = Color.FromArgb(157, 93, 0);
    private static readonly Color ErrColor = Color.FromArgb(196, 43, 28);
    private static readonly Color MutedColor = Color.FromArgb(96, 96, 96);

    private readonly TextBox _inputBox = new();
    private readonly TextBox _outputBox = new();
    private readonly TextBox _logBox = new();
    private readonly Button _btnBrowse = new();
    private readonly Button _btnBrowseOutput = new();
    private readonly Button _btnPatch = new();
    private readonly Button _btnOpenDir = new();
    private readonly Button _btnExit = new();
    private readonly Label _lblSizeValue = new();
    private readonly Label _lblVersionValue = new();
    private readonly Label _lblAssemblyValue = new();
    private readonly Label _lblStatusValue = new();
    private readonly Label _lblHint = new();

    private bool _busy;
    private bool _hasValidInput;
    private string? _lastOutputPath;

    public MainForm(string? initialInput)
    {
        Text = AppTitle;
        ClientSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Color.FromArgb(246, 248, 251);
        AllowDrop = true;

        BuildUi();

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        Shown += async (_, _) =>
        {
            if (initialInput is not null)
                await LoadInputAsync(initialInput);
        };
    }

    private void BuildUi()
    {
        // ---- header ----
        var lblTitle = new Label
        {
            Text = AppTitle,
            AutoSize = true,
            Location = new Point(20, 14),
            Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
            ForeColor = Accent
        };
        Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "第三方独立工具，与 PCL-Community、龙腾猫跃（LTCat）无隶属关系。仅用于修改你自行合法获取的官方 PCL CE 单文件 exe。",
            Location = new Point(20, 48),
            Size = new Size(764, 20),
            ForeColor = MutedColor,
            AutoEllipsis = true
        };
        Controls.Add(lblSubtitle);

        // ---- input group ----
        var grpInput = new GroupBox
        {
            Text = "第 1 步 · 选择官方 PCL CE 单文件 exe（支持从资源管理器拖拽到窗口）",
            Location = new Point(16, 76),
            Size = new Size(768, 96)
        };
        Controls.Add(grpInput);

        _inputBox.Location = new Point(14, 26);
        _inputBox.Size = new Size(620, 28);
        _inputBox.ReadOnly = true;
        _inputBox.BackColor = Color.White;
        _inputBox.PlaceholderText = "尚未选择文件";
        grpInput.Controls.Add(_inputBox);

        _btnBrowse.Text = "选择文件…";
        _btnBrowse.Location = new Point(646, 24);
        _btnBrowse.Size = new Size(108, 32);
        _btnBrowse.Click += BtnBrowse_Click;
        grpInput.Controls.Add(_btnBrowse);

        var lblInputHint = new Label
        {
            Text = "请从官方 Releases 下载 PCL2_CE_Release_x64.exe；请勿使用他人提供的“免限制版”。",
            Location = new Point(14, 64),
            Size = new Size(740, 20),
            ForeColor = MutedColor
        };
        grpInput.Controls.Add(lblInputHint);

        // ---- info group ----
        var grpInfo = CreateGroup("文件信息", 16, 182, 768, 128);
        AddKeyValue(grpInfo, "文件大小", 24, _lblSizeValue);
        AddKeyValue(grpInfo, "版本", 48, _lblVersionValue);
        AddKeyValue(grpInfo, "内嵌程序集", 72, _lblAssemblyValue);
        AddKeyValue(grpInfo, "地区限制状态", 96, _lblStatusValue);
        _lblStatusValue.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);

        // ---- output group ----
        var grpOutput = new GroupBox
        {
            Text = "第 2 步 · 输出文件（默认在原文件旁生成 .patched.exe）",
            Location = new Point(16, 320),
            Size = new Size(768, 84)
        };
        Controls.Add(grpOutput);

        _outputBox.Location = new Point(14, 26);
        _outputBox.Size = new Size(620, 28);
        _outputBox.BackColor = Color.White;
        grpOutput.Controls.Add(_outputBox);

        _btnBrowseOutput.Text = "另存为…";
        _btnBrowseOutput.Location = new Point(646, 24);
        _btnBrowseOutput.Size = new Size(108, 32);
        _btnBrowseOutput.Click += BtnBrowseOutput_Click;
        grpOutput.Controls.Add(_btnBrowseOutput);

        // ---- action buttons ----
        _btnPatch.Text = "开始打补丁";
        _btnPatch.Location = new Point(16, 416);
        _btnPatch.Size = new Size(168, 42);
        _btnPatch.Enabled = false;
        _btnPatch.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        _btnPatch.BackColor = Accent;
        _btnPatch.ForeColor = Color.White;
        _btnPatch.FlatStyle = FlatStyle.Flat;
        _btnPatch.FlatAppearance.BorderSize = 0;
        _btnPatch.Click += BtnPatch_Click;
        Controls.Add(_btnPatch);

        _btnOpenDir.Text = "打开输出位置";
        _btnOpenDir.Location = new Point(196, 416);
        _btnOpenDir.Size = new Size(140, 42);
        _btnOpenDir.Enabled = false;
        _btnOpenDir.Click += (_, _) => OpenOutputLocation();
        Controls.Add(_btnOpenDir);

        _btnExit.Text = "退出";
        _btnExit.Location = new Point(660, 416);
        _btnExit.Size = new Size(124, 42);
        _btnExit.Click += (_, _) => Close();
        Controls.Add(_btnExit);

        // ---- log ----
        var grpLog = new GroupBox
        {
            Text = "日志",
            Location = new Point(16, 470),
            Size = new Size(768, 86)
        };
        Controls.Add(grpLog);

        _logBox.Location = new Point(14, 20);
        _logBox.Size = new Size(740, 56);
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Font = new Font("Consolas", 8.5F);
        _logBox.BackColor = Color.White;
        grpLog.Controls.Add(_logBox);

        // ---- footer ----
        _lblHint.Text = "补丁后的文件是官方软件的修改副本：仅供个人使用，请勿再分发。使用前请阅读 README / DISCLAIMER。";
        _lblHint.Location = new Point(16, 564);
        _lblHint.Size = new Size(768, 20);
        _lblHint.ForeColor = MutedColor;
        Controls.Add(_lblHint);

        Log($"PCL CE 区域限制补丁工具 v{AppVersion} · 仅修改本地官方单文件 exe，不接触官方密钥。");
    }

    private GroupBox CreateGroup(string text, int x, int y, int width, int height)
    {
        var group = new GroupBox
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height)
        };
        Controls.Add(group);
        return group;
    }

    private static void AddKeyValue(GroupBox group, string key, int y, Label valueLabel)
    {
        var keyLabel = new Label
        {
            Text = key,
            Location = new Point(16, y + 3),
            Size = new Size(100, 20),
            ForeColor = MutedColor
        };
        group.Controls.Add(keyLabel);

        valueLabel.Location = new Point(120, y + 3);
        valueLabel.Size = new Size(632, 20);
        valueLabel.AutoEllipsis = true;
        group.Controls.Add(valueLabel);
    }

    // ------------------------------------------------------------------ actions

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择官方 PCL CE 单文件 exe",
            Filter = "PCL CE 可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _ = LoadInputAsync(dialog.FileName);
    }

    private void BtnBrowseOutput_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_inputBox.Text))
        {
            MessageBox.Show(this, "请先选择官方 exe 文件。", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "选择输出文件位置",
            Filter = "可执行文件 (*.exe)|*.exe",
            FileName = Path.GetFileName(_outputBox.Text),
            InitialDirectory = Path.GetDirectoryName(_outputBox.Text)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _outputBox.Text = dialog.FileName;
    }

    private async Task LoadInputAsync(string path)
    {
        if (_busy) return;

        _busy = true;
        _hasValidInput = false;
        _btnPatch.Enabled = false;
        _btnOpenDir.Enabled = false;
        _inputBox.Text = path;
        SetStatus("正在读取并检查文件…", WarnColor);
        _lblSizeValue.Text = "—";
        _lblVersionValue.Text = "—";
        _lblAssemblyValue.Text = "—";

        try
        {
            var version = GetFileVersion(path);
            var result = await Task.Run(() =>
            {
                var data = File.ReadAllBytes(path);
                return (Length: data.Length, Inspection: PatchEngine.Inspect(data));
            });

            var inspection = result.Inspection;
            _lblSizeValue.Text = $"{result.Length:N0} 字节";
            _lblVersionValue.Text = version;
            _lblAssemblyValue.Text =
                $"偏移 0x{inspection.AssemblyStart:X} · {inspection.ImageSize:N0} 字节 · " +
                $"{inspection.SectionCount} 个节 · IL-only = {inspection.IsIlOnly}";

            if (inspection.IsPatched)
            {
                SetStatus("✔ 已解除地区限制（该文件已经打过补丁）", OkColor);
                Log($"检测：{path}");
                Log("状态：该文件已打过补丁，地区限制已解除。");
            }
            else
            {
                SetStatus("尚未解除地区限制（可以进行打补丁）", WarnColor);
                Log($"检测：{path}");
                Log($"内嵌程序集位于文件偏移 0x{inspection.AssemblyStart:X}，共 {inspection.MethodCount} 个目标方法。");
            }

            foreach (var method in inspection.Methods)
                Log("  " + method);

            _outputBox.Text = BuildDefaultOutputPath(path);
            _hasValidInput = true;
            _btnPatch.Enabled = true;
            _btnPatch.Text = inspection.IsPatched ? "重新打补丁" : "开始打补丁";
        }
        catch (PatchNotSupportedException ex)
        {
            _lblAssemblyValue.Text = "—";
            SetStatus("不支持的文件", ErrColor);
            Log("错误：" + ex.Message);
            MessageBox.Show(this, ex.Message, "不支持的文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _lblAssemblyValue.Text = "—";
            SetStatus("读取文件失败", ErrColor);
            Log("错误：" + ex.Message);
            MessageBox.Show(this, "读取文件失败：" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void BtnPatch_Click(object? sender, EventArgs e)
    {
        if (_busy || !_hasValidInput) return;

        var input = _inputBox.Text;
        var output = _outputBox.Text.Trim();

        if (!File.Exists(input))
        {
            MessageBox.Show(this, "输入文件不存在，请重新选择。", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            output = BuildDefaultOutputPath(input);
            _outputBox.Text = output;
        }

        output = Path.GetFullPath(output);
        if (string.Equals(Path.GetFullPath(input), output, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "输出文件不能与输入文件相同。", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (File.Exists(output) &&
            MessageBox.Show(this, $"输出文件已存在：\r\n{output}\r\n\r\n是否覆盖？", AppTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _busy = true;
        _btnPatch.Enabled = false;
        _btnOpenDir.Enabled = false;
        SetStatus("正在打补丁，请稍候…", WarnColor);
        Log("开始打补丁…");

        try
        {
            var patch = await Task.Run(() =>
            {
                var data = File.ReadAllBytes(input);
                var result = PatchEngine.Apply(data);
                File.WriteAllBytes(output, data);
                return result;
            });

            foreach (var line in patch.PatchedMethods)
                Log("patched: " + line);
            foreach (var line in patch.Verification)
                Log("verify : " + line);

            Log($"已生成：{output}");
            Log("提示：该文件是官方软件的修改副本，请仅个人使用，不要分发给他人。");
            SetStatus("✔ 补丁成功，地区限制已解除", OkColor);
            _lastOutputPath = output;
            _btnOpenDir.Enabled = true;
            _btnPatch.Text = "重新打补丁";

            var generated = new FileInfo(output);
            _lblHint.Text = $"补丁成功：{generated.Name}（{generated.Length:N0} 字节）。补丁后的文件仅供个人使用，请勿再分发。";
        }
        catch (PatchNotSupportedException ex)
        {
            SetStatus("补丁失败", ErrColor);
            Log("错误：" + ex.Message);
            MessageBox.Show(this, ex.Message, "补丁失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            SetStatus("补丁失败", ErrColor);
            Log("错误：" + ex.Message);
            MessageBox.Show(this, "补丁失败：" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            _btnPatch.Enabled = _hasValidInput;
        }
    }

    private void OpenOutputLocation()
    {
        var path = _lastOutputPath ?? _outputBox.Text.Trim();
        try
        {
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else if (Directory.Exists(Path.GetDirectoryName(path)))
                Process.Start(new ProcessStartInfo("explorer.exe", Path.GetDirectoryName(path)!) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法打开文件夹：" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static string BuildDefaultOutputPath(string input)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(input)) ?? Environment.CurrentDirectory;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(input) + ".patched.exe");
    }

    private static string GetFileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var fileVersion = string.IsNullOrWhiteSpace(info.FileVersion) ? "未知" : info.FileVersion;
            var productVersion = string.IsNullOrWhiteSpace(info.ProductVersion) ? string.Empty : $"（{info.ProductVersion}）";
            return fileVersion + productVersion;
        }
        catch
        {
            return "未知";
        }
    }

    private void SetStatus(string text, Color color)
    {
        _lblStatusValue.Text = text;
        _lblStatusValue.ForeColor = color;
    }

    private void Log(string message)
    {
        if (_logBox.TextLength > 0) _logBox.AppendText(Environment.NewLine);
        _logBox.AppendText(message);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true &&
            e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files &&
            files[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            await LoadInputAsync(files[0]);
    }
}
