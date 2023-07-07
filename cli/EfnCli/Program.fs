open ProcessArguments
open Argu

[<EntryPoint>]
let main argv = 
    try
        // processArguments [|"-n"; "Hello"; "../../../"|]

        processArguments argv
        0
    with
    | :? ArguParseException as ex ->
        printfn "%s" ex.Message
        0