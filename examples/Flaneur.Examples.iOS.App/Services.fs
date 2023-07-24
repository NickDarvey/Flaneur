namespace Flaneur.Examples.iOS.App.Services

open System

type SearchResult = {URI: string; Title: string}

type SearchService = 
  abstract search: term:string -> offset: int -> count: int -> IObservable<SearchResult list>
  abstract login: unit -> IObservable<unit> 
  abstract cat : int
