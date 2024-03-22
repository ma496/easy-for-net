module ProcessArguments

open System
open System.IO
open Argu
open Arguments
open RepositoryClone
open Helpers
open System.Text.RegularExpressions

let private prepareArguments (args: string array) =
    let mutable arguments = Array.copy args |> Array.toList

    if arguments.Length >= 2 && (arguments |> List.contains "-n" || arguments |> List.contains "--new") then
        let newParamIndex = arguments |> List.findIndex(fun x -> x = "-n" || x = "--new")
        let newParamPathIndex = newParamIndex + 2
        if newParamPathIndex < arguments.Length then
            if arguments[newParamPathIndex].StartsWith("-") || arguments[newParamPathIndex].StartsWith("--") then
                arguments <- arguments |> List.insertAt newParamPathIndex "./"
                arguments |> List.toArray
            else
                arguments |> List.toArray
        else
            arguments <- arguments |> List.insertAt newParamPathIndex "./"
            arguments |> List.toArray
    else
        arguments |> List.toArray

let private createProject (path: string) (name: string) =
    printfn "Creating the project (%s)" name

    let sourcePath = Path.Combine(getTemplateDirectory (), "Latest", "backends", "CSharpBackend")

    let destinationPath = Path.Combine(Directory.GetCurrentDirectory(), path, name)

    if Directory.Exists(destinationPath) then
        Exception($"{Path.GetFullPath destinationPath} directory already exist") |> raise

    copyDirectoryRecursively sourcePath destinationPath false

let private modifySlnIds (path: string) =
    let mutable content = File.ReadAllText path
    let regex = new Regex(@"\{([^{}]+)\}")
    let idMatches = regex.Matches(content)
    printfn "%d" idMatches.Count

    let ids =
        idMatches
        |> Seq.map(fun m -> m.Groups[1].Value)
        |> Seq.distinct
        |> Seq.toArray
    printfn "%d" ids.Length

    ids
    |> Array.iter(fun id -> 
        let newId = Guid.NewGuid().ToString()
        content <- content.Replace(id, newId)
    )

    File.WriteAllText(path, content)

let private renameSlnFile (path: string) (name: string) =
    let slnFileName = "CSharpBackend.sln"
    let slnFilePath = Path.Combine(Directory.GetCurrentDirectory(), path, name, slnFileName)
    let slnFile = FileInfo(slnFilePath)
    slnFile.MoveTo(slnFile.FullName.Replace(slnFileName, $"{name}.sln"))

let private modifyFileContent (path: string) (oldText: string) (newNext: string) =
    let mutable content = File.ReadAllText path
    content <- content.Replace(oldText, newNext)
    File.WriteAllText(path, content)

let rec private renameAndModify (path: string) (oldText: string) (newText: string) =
    let directory = DirectoryInfo(path)

    directory.GetFiles()
    |> Array.iter(fun f -> 
        modifyFileContent f.FullName oldText newText

        // rename file
        if f.Name.Contains(oldText) then
            f.MoveTo(f.FullName.Replace(oldText, newText))
    )

    for subdirectory in directory.GetDirectories() do
        renameAndModify subdirectory.FullName oldText newText

let private processNewArgument (path: string) (name: string) =
    clone()
    createProject path name 
    renameSlnFile path name
    renameAndModify (Path.Combine(path, name)) "EasyForNet" name
    Console.ForegroundColor <- ConsoleColor.Green
    printfn "Successfully created the project (%s)" name
    Console.ResetColor()

let processArguments args = 
    let arguments = prepareArguments args
    let parser = ArgumentParser.Create<CliArguments>(programName = "efn")
    // let usage = parser.PrintUsage()
    // printfn "%s" usage

    let parseResults = parser.ParseCommandLine arguments

    parseResults.GetAllResults() 
    |> List.iter (fun r -> 
        match r with
        | New(n, p) -> processNewArgument p n
    )