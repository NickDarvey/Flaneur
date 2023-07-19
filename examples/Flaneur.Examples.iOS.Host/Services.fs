module Services

open System
open Flaneur.Remoting.Attributes


type SearchResult = {URI: string; Titled: string}

[<RemoteInterface>]
type RemoteServices = 
  abstract search: term:string -> offset: int -> count: int -> IObservable<SearchResult list>
  abstract login: unit -> IObservable<unit> 

[<RemoteImplementation>]
let service = {
  new RemoteServices with
    member _.search term offset count = invalidOp "Not implemented"
    member _.login () = invalidOp "Not implemented"
}