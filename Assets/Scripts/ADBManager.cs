using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ADBManager : MonoBehaviour
{
    private string[] AppIds = new[] { "App1", "App2", "App3" };
    private static string _adb;
    public string appId => appsDropdown.options[appsDropdown.value].text;

    public string apkPath;
    public TMP_InputField ConnectedDevice;
    public TMP_InputField adbPathField;
    public TMP_Dropdown appsDropdown;

    private void Start()
    {
        adbPathField.text = ADBPath;
    }

    public string ADBPath
    {
        get => PlayerPrefs.GetString("ADBPath", "");
        set => PlayerPrefs.SetString("ADBPath", value);
    }

    public string ADB
    {
        get
        {
            if (string.IsNullOrEmpty(_adb)) _adb = GetAdbPath();
            return _adb;
        }
    }

    public static string LastBuiltApk
    {
        get => PlayerPrefs.GetString("builtApkPath");
        set => PlayerPrefs.SetString("builtApkPath", value);
    }

    private string GetAdbPath()
    {
// #if UNITY_EDITOR_OSX
//             string path = EditorApplication.applicationPath;
//             path = path.Replace("Unity.app", "PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb");
//             return path;
// #else
//         string path = EditorApplication.applicationContentsPath +
//                       "/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe";
//         return "\"" + path + "\"";
// #endif
        ADBPath = adbPathField.text;
        return adbPathField.text;
    }

    public void RefreshList() => Refresh();

    private async void Refresh()
    {
        Debug.Log("Refreshing");

        var output = await GetOutput(ExecuteCommand($"{ADB} devices -l"));

        var lines = output.output.Split('\n');
        ConnectedDevice.text = lines[1];

        output = await GetOutput(ExecuteCommand($"{ADB} shell cmd package list packages"));
        lines = console.Split('\n');

        AppIds = lines
            .Where(l => l.StartsWith("package:com."))
            .Select(l => l.Replace("package:", ""))
            .ToArray();

        // If it contains the current project app then select it
        // if (AppIds.Contains(Application.identifier)) appId = Application.identifier;
        // else appId = AppIds[0];

        apkPath = LastBuiltApk;
        PopulateDropdown(appsDropdown, AppIds);
        print("CurrentSelectedAPP " + appId);
    }

    public static Process ExecuteCommand(string command)
    {
        Debug.Log($"Executing command <color=green> {command} </color>");
        string fileName = "cmd.exe";
        string arguments = $"/C {command.Trim()}";

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        var firstSpace = command.IndexOf(' ') + 1;
        fileName = command.Substring(0, firstSpace).Trim();
        arguments = $"{command.Substring(firstSpace).Trim()}";
#endif

        Process cmd = new Process();
        cmd.StartInfo.FileName = fileName;
        cmd.StartInfo.Arguments = arguments;
        cmd.StartInfo.RedirectStandardInput = true;
        cmd.StartInfo.RedirectStandardOutput = true;
        cmd.StartInfo.RedirectStandardError = true;
        cmd.StartInfo.CreateNoWindow = true;
        cmd.StartInfo.UseShellExecute = false;
        cmd.Start();

        return cmd;
    }

    private async Task<(string output, string error)> GetOutput(Process cmd)
    {
        var builder = new StringBuilder();
        while (!cmd.HasExited)
        {
            var line = await cmd.StandardOutput.ReadLineAsync();
            builder.AppendLine(line);
            WriteLine(line);
        }

        var text = await cmd.StandardOutput.ReadToEndAsync();
        builder.Append(text);
        WriteLine(text);
        ScrollToLast();

        return (builder.ToString(), await cmd.StandardError.ReadToEndAsync());
    }

    private async void InstallApk()
    {
        if (!File.Exists(apkPath)) return;
        var output = await GetOutput(ExecuteCommand($"{ADB} install {apkPath}"));
    }

    private Vector2 ScrollPos;
    private string console;

    private readonly StringBuilder _builder = new StringBuilder();
    private void WriteLine(string text) => console = _builder.AppendLine(text).ToString();
    private void ScrollToLast() => ScrollPos.y = float.MaxValue;
    private void Clear() => console = _builder.Clear().ToString();

    private void PopulateDropdown(TMP_Dropdown dropdown, string[] optionsArray)
    {
        var options = optionsArray.ToList();
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
    }

    public void OpenAppButton()
    {
        OpenApp(appId);
    }

    public void CloseAppButton()
    {
        CloseApp(appId);
    }

    public void ResetAppButton()
    {
        ResetApp(appId);
    }

    public void UninstallAppButton()
    {
        UninstallApp(appId);
    }

    private async void UninstallApp(string packageId)
    {
        var output = await GetOutput(ExecuteCommand($"{ADB} uninstall {packageId}"));
        Debug.Log(output);
    }

    private async void OpenApp(string packageId)
    {
        var output =
            await GetOutput(
                ExecuteCommand($"{ADB} shell monkey -p {packageId} -c android.intent.category.LAUNCHER 1"));
        Debug.Log(output);
    }

    private async void CloseApp(string packageId)
    {
        var output = await GetOutput(ExecuteCommand($"{ADB} shell am force-stop {packageId}"));
        Debug.Log(output);
    }

    private async void ResetApp(string packageId)
    {
        var output = await GetOutput(ExecuteCommand($"{ADB} shell pm clear {packageId}"));
        Debug.Log(output);
    }
}