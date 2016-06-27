using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Put the file to Assets\Plugins\Editor

namespace Assets.Plugins.Editor
{
  [InitializeOnLoad]
  public static class Rider
  {
    private static readonly string SlnFile;
    private static readonly string defaultApp = EditorPrefs.GetString("kScriptsDefaultApp");

    static Rider()
    {

      if (string.IsNullOrEmpty(defaultApp))
        return;
      var riderFileInfo = new FileInfo(defaultApp);
      if (riderFileInfo.FullName.ToLower().Contains("rider") &&
          (!riderFileInfo.Exists || riderFileInfo.Extension == ".app")) // seems like app doesn't exist as file
      {
        var newPath = riderFileInfo.FullName;
        // try to search the new version

        switch (riderFileInfo.Extension)
        {

          /*
              Unity itself transforms lnk to exe
              case ".lnk":
              {
                if (riderFileInfo.Directory != null && riderFileInfo.Directory.Exists)
                {
                  var possibleNew = riderFileInfo.Directory.GetFiles("*ider*.lnk");
                  if (possibleNew.Length > 0)
                    newPath = possibleNew.OrderBy(a => a.LastWriteTime).Last().FullName;
                }
                break;
              }*/
          case ".exe":
          {
            var possibleNew =
              riderFileInfo.Directory.Parent.Parent.GetDirectories("*ider*")
                .SelectMany(a => a.GetDirectories("bin")).SelectMany(a => a.GetFiles(riderFileInfo.Name))
                .ToArray();
            if (possibleNew.Length > 0)
              newPath = possibleNew.OrderBy(a => a.LastWriteTime).Last().FullName;
            break;
          }
          case ".app": //osx
          {
            break;
          }
          default:
          {
            Debug.Log(
              "Please manually update the path to Rider in Unity Preferences -> External Tools -> External Script Editor.");
            break;
          }
        }
        if (newPath != riderFileInfo.FullName)
        {
          Debug.Log(riderFileInfo.FullName);
          Debug.Log(newPath);
          EditorPrefs.SetString("kScriptsDefaultApp", newPath);
        }
      }


      // Open the solution file
      var projectDirectory = Directory.GetParent(Application.dataPath).FullName;
      var projectName = Path.GetFileName(projectDirectory);
      SlnFile = Path.Combine(projectDirectory, string.Format("{0}.sln", projectName));
    }

    /// <summary>
    /// Asset Open Callback (from Unity)
    /// </summary>
    /// <remarks>
    /// Called when Unity is about to open an asset.
    /// </remarks>
    [UnityEditor.Callbacks.OnOpenAssetAttribute()]
    static bool OnOpenedAsset(int instanceID, int line)
    {
      if (string.IsNullOrEmpty(defaultApp))
        return false;

      var riderFileInfo = new FileInfo(defaultApp);
      if (riderFileInfo.FullName.ToLower().Contains("rider") &&
          (riderFileInfo.Exists || riderFileInfo.Extension == ".app"))
      {
        string appPath = Path.GetDirectoryName(Application.dataPath);

        // determine asset that has been double clicked in the project view
        var selected = EditorUtility.InstanceIDToObject(instanceID);

        if (selected.GetType().ToString() == "UnityEditor.MonoScript" ||
            selected.GetType().ToString() == "UnityEngine.Shader")
        {
          var completeFilepath = appPath + Path.DirectorySeparatorChar + AssetDatabase.GetAssetPath(selected);
          var args = string.Empty;
          if (GetPossibleRiderProcess() != null)
            args = string.Format(" -l {2} {0}{3}{0}", "\"", SlnFile, line, completeFilepath);
          else
            args = string.Format("{0}{1}{0} -l {2} {0}{3}{0}", "\"", SlnFile, line, completeFilepath);

          CallRider(riderFileInfo.FullName, args);
          return true;
        }
      }
      return false;
    }

    private static void CallRider(string riderPath, string args)
    {
      var proc = new Process();
      if (new FileInfo(riderPath).Extension == ".app")
      {
        proc.StartInfo.FileName = "open";
        proc.StartInfo.Arguments = string.Format("-n {0}{1}{0} --args {2}", "\"", "/" + riderPath, args);
        Debug.Log(proc.StartInfo.FileName + " " + proc.StartInfo.Arguments);
      }
      else
      {
        proc.StartInfo.FileName = riderPath;
        proc.StartInfo.Arguments = args;
        Debug.Log("\"" + proc.StartInfo.FileName + "\"" + " " + proc.StartInfo.Arguments);
      }

      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
      proc.StartInfo.CreateNoWindow = true;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.Start();
    }

    private static Process GetPossibleRiderProcess()
    {
      var riderProcesses = Process.GetProcesses();
      foreach (var riderProcess in riderProcesses)
            {
              try
              {
                if (riderProcess.ProcessName.ToLower().Contains("rider"))
                  return riderProcess;
              }
              catch (Exception)
              {
              }
            }
      return null;
    }
  }

  public class RiderAssetPostprocessor : AssetPostprocessor
  {
    public static void OnGeneratedCSProjectFiles()
    {
      var currentDirectory = Directory.GetCurrentDirectory();
      var projectFiles = Directory.GetFiles(currentDirectory, "*.csproj");

      bool isModified = false;
      foreach (var file in projectFiles)
      {
        string content = File.ReadAllText(file);
        if (content.Contains("<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>"))
        {
          content = Regex.Replace(content, "<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>",
            "<TargetFrameworkVersion>v4.5</TargetFrameworkVersion>");
          File.WriteAllText(file, content);
          isModified = true;
        }
      }

      Debug.Log(isModified ? "Project was post processed successfully" : "No change necessary in project");
    }
  }
}

// Developed using JetBrains Rider =)