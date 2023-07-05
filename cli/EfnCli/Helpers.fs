module Helpers
open System
open System.IO

let getTemplateDirectory () =
    let appDataPath = Environment.SpecialFolder.ApplicationData |> Environment.GetFolderPath
    let dir = $"{appDataPath}\\Efn\\Template"

    if not <| Directory.Exists(dir) then
        Directory.CreateDirectory(dir) |> ignore

    dir

let rec copyDirectoryRecursively sourcePath destinationPath  =
    let sourceDirectory = DirectoryInfo(sourcePath);
    let destinationDirectory = DirectoryInfo(destinationPath)

    if not (destinationDirectory.Exists) then
        Directory.CreateDirectory(destinationPath) |> ignore

    for file in sourceDirectory.GetFiles() do
        let destinationFilePath = Path.Combine(destinationPath, file.Name)
        file.CopyTo(destinationFilePath, true) |> ignore

    for subdirectory in sourceDirectory.GetDirectories() do
        let newDestinationPath = Path.Combine(destinationPath, subdirectory.Name)
        copyDirectoryRecursively subdirectory.FullName newDestinationPath

let cleanTemplate dir =
    printfn "%s" "Clean the template"

    let gitDir = dir + "\\.git"
    if Directory.Exists gitDir then
        let dirInfo = DirectoryInfo(gitDir)
        dirInfo.Delete(true)

    let githubDir = $"{dir}\\.github"
    if Directory.Exists githubDir then
        let dirInfo = DirectoryInfo(githubDir)
        dirInfo.Delete(true)