namespace Flaneur.Remoting

open System

type Encoder<'Encoded> =
  abstract Encode : 'Value -> 'Encoded

type Decoder<'Encoded> =
  abstract Decode : 'Encoded -> 'Value

/// A Flaneur proxy for sending remote invocations to the host.
type Proxy =
  abstract Invoke : action:string -> IObservable<'Result>
  abstract Invoke : action:string * param0:(string* 'Parameter0) -> IObservable<'Result>
  abstract Invoke : action:string * param0:(string* 'Parameter0) * param1:(string* 'Parameter1) -> IObservable<'Result>

/// A Flaneur handler for handling remote invocations from the app.
type Handler<'Parameter, 'Result> = Decoder<'Parameter> -> Encoder<'Result> -> string (*action name*) -> 'Parameter list -> IObservable<'Result>
