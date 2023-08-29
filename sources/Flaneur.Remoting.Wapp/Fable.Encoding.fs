module internal Fable.Encoding

open Fable.Core
open Fable.Core.JS
open Fable.Streams

/// The TextDecoderStream interface of the Encoding API converts a stream of text in a binary encoding, such as UTF-8 etc., to a stream of strings. It is the streaming equivalent of TextDecoder.
/// 
/// https://developer.mozilla.org/en-US/docs/Web/API/TextDecoderStream
type TextDecoderStream =
  inherit TransformStream<Uint8Array, string>

// https://fable.io/docs/javascript/features.html#paramobject
[<Global; AllowNullLiteral>]
type TextDecoderStreamOptions
    [<ParamObject; Emit("$0")>]
    (
        ?fatal: bool
    ) =
    member val fatal: bool option = jsNative with get, set

type TextDecoderStreamType =
  [<Emit("new $0($1...)")>] abstract Create: ?label : string * ?options : TextDecoderStreamOptions -> TextDecoderStream

let [<Global>] TextDecoderStream : TextDecoderStreamType = jsNative