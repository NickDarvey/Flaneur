namespace Flaneur.Launching.MSBuild

open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open System
open System.Diagnostics
open System.Threading
open System.Runtime.InteropServices

type SetEnvTask() =
  inherit Task()

  [<Required>]
  member val Variable = null with get, set

  [<Required>]
  member val Value = null with get, set

  override this.Execute() =
    Environment.SetEnvironmentVariable (this.Variable, this.Value)
    true


type LaunchTask() =
  inherit Task()

  let mutable kill = ignore
  let cancel = new CancellationTokenSource()

  interface ICancelableTask with
    member _.Cancel() = cancel.Cancel()

  [<Required>]
  member val Command = null with get, set

  member val WorkingDirectory = null with get, set

  override this.Execute() =
    if String.IsNullOrWhiteSpace this.Command then
      this.Log.LogError ($"LaunchTask requires a non-empty Command parameter.")
      false
    else
      let shell, args =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        then "cmd.exe", [ "/k"; this.Command ]
        else "/bin/sh", [ this.Command ]
      let proc = new Process (
        StartInfo = new ProcessStartInfo (
          CreateNoWindow = false,
          FileName = shell,
          Arguments = String.concat " " args,
          WorkingDirectory = this.WorkingDirectory
        )
      )
      do kill <- proc.Dispose
      proc.Start ()

type DeepestCommonDirectoryTask() =
  inherit Task()
  
  let getLongestCommonPrefix (strings : string[]) : string =
    if Array.isEmpty strings then "" else

    let rec charsf stringi chari charmax =
      if chari = charmax then chari
      elif strings.[0].[chari] = strings.[stringi].[chari]
      then charsf stringi (chari + 1) charmax
      else chari

    let rec stringsf stringi chari stringmax =
      if stringi = stringmax then chari else
      let chari = min chari strings.[stringi].Length
      let chari = charsf stringi 0 chari
      stringsf (stringi + 1) chari stringmax

    let i = strings.[0].Length
    let i = stringsf 1 i strings.Length

    strings.[0].[0..i-1]

  [<Required>]
  member val Items = Array.empty<ITaskItem> with get, set

  [<Output>]
  member val Directory = null with get, set

  override this.Execute() =
    let items = this.Items |> Array.map (fun i -> i.GetMetadata "FullPath")
    let prefix = getLongestCommonPrefix items
    let last = prefix.LastIndexOf System.IO.Path.DirectorySeparatorChar
    this.Directory <- if last < 0 then "" else prefix.Substring(0, last)
    true


    // https://github.com/stazz/UtilPack/blob/7d9fb1bda314c818eb9076cdb4ff1f048544b03d/Source/Code/UtilPack.MSBuild.AsyncExec/AsyncExec.cs#L27

    // launch vs build gets compiled in
    // https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md
    // means that if you build, then hit run, it'll run the build version not the launch version
    // could disable if confusing
    // https://stackoverflow.com/questions/1937702/visual-studio-run-c-project-post-build-event-even-if-project-is-up-to-date/49639051#49639051
    // or maybe inlude the 'islaunch' property into the calculation of isuptodate