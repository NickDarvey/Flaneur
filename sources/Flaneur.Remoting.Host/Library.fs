namespace Flaneur.Remoting

open System

type Handler<'Parameter, 'Result> =
  string -> 'Parameter array -> IObservable<'Result>