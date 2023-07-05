module Arguments

open Argu

type CliArguments =
    | [<AltCommandLine("-n")>] [<Unique>] New of name:string * path: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | New _ -> "specify new project."