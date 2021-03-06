﻿[<AutoOpen>]
module internal Equal.Primitives

open System
open FParsec

let parenthesize parser =
    parser
    |> between spaces spaces
    |> between (skipChar '(') (skipChar ')')

[<GeneralizableValue>]
let literal<'u> : Parser<string, 'u> =
    let apostrophe  = stringReturn "''" '\''
    let otherSymbol = satisfy ((<>) '\'')

    manyChars (apostrophe <|> otherSymbol)
    |> between (skipChar '\'') (skipChar '\'')
    .>> spaces

[<GeneralizableValue>]
let ident<'u> : Parser<string, 'u> =
    identifier(IdentifierOptions()) .>> spaces <?> "property"

[<GeneralizableValue>]
let pdecimal<'u> : Parser<decimal, 'u> =
    pfloat |>> decimal <?> "decimal number"

let private toConst (parserFunc: string -> bool * 'a) (p: Parser<string, 'u>) : Parser<'a, 'u> =
    let err = fun _ -> Reply(Error, expected typeof<'a>.Name)
    fun stream ->
        let initState = stream.State
        let v = p stream
        match v.Status with
        | Ok ->
            match parserFunc v.Result with
            | true, res ->
                Reply res
            | false, _ ->
                stream.BacktrackTo initState
                err stream
        | err -> Reply(err, v.Error)

let private pconst tryParse : Parser<'a, 'u> = literal |> toConst tryParse

[<GeneralizableValue>]
let pdatetime<'u> : Parser<DateTime, 'u> = pconst DateTime.TryParse

[<GeneralizableValue>]
let pdatetimeoffset<'u> : Parser<DateTimeOffset, 'u> = pconst DateTimeOffset.TryParse

[<GeneralizableValue>]
let penum<'a, 'b, 'u when 'a: enum<'b> and 'a : (new: unit -> 'a) and 'a : struct and 'a :> ValueType> : Parser<'a, 'u> =
    let identOrDigit = ident <|> regex "\d+" <?> typeof<'a>.Name
    identOrDigit |> toConst (fun x -> Enum.TryParse(x, ignoreCase = true))