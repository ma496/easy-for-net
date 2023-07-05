module RepositoryClone

open System.Diagnostics
open System.IO
open Helpers

let private cloneUsingCommand dir =
    let latestDir = $"{dir}\\Latest"
    if Directory.Exists(latestDir) |> not then
        "Downloading the template" |> printfn "%s"
        let startInfo = new ProcessStartInfo()
        startInfo.FileName <- "git"
        startInfo.Arguments <- $"clone https://github.com/ma496/easy-for-net.git Latest"
        startInfo.WorkingDirectory <- dir
        let processCmd = new Process(StartInfo = startInfo)
        processCmd.Start() |> ignore
        processCmd.WaitForExit()
        // cleanTemplate latestDir
    else
        "Use the existing template" |> printfn "%s"

let clone () =
    getTemplateDirectory() |> cloneUsingCommand |> ignore
    