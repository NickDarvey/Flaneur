module Services

open System

type SearchResult = {URI: string; Title: string}

[<RequireQualifiedAccess>]
type RemoteServices = 
  abstract search: term:string -> offset: int -> count: int -> IObservable<SearchResult list>
  abstract login: unit -> IObservable<unit> 
