module ProcessArguments

open System.IO
open Argu
open Arguments
open RepositoryClone
open Helpers

let private createProject (name: string) (path: string) =
    printfn "Creating the project (%s)" name

    let sourcePath =
        Path.Combine(getTemplateDirectory (), "Latest", "backends", "c-sharp")

    let destinationPath = Path.Combine(Directory.GetCurrentDirectory(), path, name)
        
    copyDirectoryRecursively sourcePath destinationPath

    printfn "Successfully created the project (%s)" name

let private processNewArgument (name: string) (path: string) =
    clone()
    createProject name path

let processArguments argv = 
    printfn "%A" argv
    let parser = ArgumentParser.Create<CliArguments>(programName = "efn")
    let usage = parser.PrintUsage()
    printfn "%s" usage

    // let parseResults = parser.ParseCommandLine argv

    // parseResults.GetAllResults() 
    // |> List.iter (fun r -> 
    //     match r with
    //     | New(n, p) -> processNewArgument n p

    // )