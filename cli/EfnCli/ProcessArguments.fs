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

let private createProject (name: string) (path: string) =
    printfn "Creating the project (%s)" name

    let sourcePath =
        Path.Combine(getTemplateDirectory (), "Latest", "backends", "c-sharp")

    let destinationPath = Path.Combine(Directory.GetCurrentDirectory(), path, name)
        
    copyDirectoryRecursively sourcePath destinationPath

let private modifySlnFile (path: string) =
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

let private modifyFileContent (path: string) (oldText: string) (newNext: string) =
    let mutable content = File.ReadAllText path
    content <- content.Replace(oldText, newNext)
    File.WriteAllText(path, content)

let rec private renameProject (name: string) (path: string) =
    let wordToChange = "CSharpBackend"
    let path = Path.Combine(Directory.GetCurrentDirectory(), path, name)
    let directory = DirectoryInfo(path)

    directory.GetFiles()
    |> Array.iter(fun f -> 
        // modify .sln file
        // if f.Name = $"{wordToChange}.sln" then
        //     modifySlnFile f.FullName

        modifyFileContent f.FullName wordToChange name

        // rename file
        if f.Name.Contains(wordToChange) then
            f.MoveTo(f.FullName.Replace(wordToChange, name), true)
    )

    for subdirectory in directory.GetDirectories() do
        let newPath = Path.Combine(path, subdirectory.Name)
        renameProject subdirectory.FullName newPath

let private processNewArgument (name: string) (path: string) =
    clone()
    createProject name path
    renameProject name path
    printfn "Successfully created the project (%s)" name

let processArguments args = 
    let arguments = prepareArguments args
    let parser = ArgumentParser.Create<CliArguments>(programName = "efn")
    // let usage = parser.PrintUsage()
    // printfn "%s" usage

    let parseResults = parser.ParseCommandLine arguments

    parseResults.GetAllResults() 
    |> List.iter (fun r -> 
        match r with
        | New(n, p) -> processNewArgument n p
    )