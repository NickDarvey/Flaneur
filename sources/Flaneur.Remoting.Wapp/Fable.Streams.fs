module internal Fable.Streams

open Fable.Core
open Fable.Core.JS
open Fetch

type ReadableStreamReadResult<'T> =
  abstract value : 'T with get
  abstract ``done`` : bool with get

/// https://streams.spec.whatwg.org/#generic-reader-mixin
type ReadableStreamGenericReader =
  abstract cancel : ?reason:string -> Promise<unit>
  abstract closed : Promise<unit>

/// https://streams.spec.whatwg.org/#default-reader-class
type ReadableStreamDefaultReader<'T> =
  inherit ReadableStreamGenericReader

  abstract read : unit -> Promise<ReadableStreamReadResult<'T>>
  abstract releaseLock : unit -> unit

// https://fable.io/docs/javascript/features.html#paramobject
[<Global; AllowNullLiteral>]
type StreamPipeOptions
    [<ParamObject; Emit("$0")>]
    (
        ?preventClose: bool, 
        ?preventAbort: bool,
        ?preventCancel: bool,
        ?signal : AbortSignal
    ) =
    member val preventClose: bool option = jsNative with get, set
    member val preventAbort: bool option = jsNative with get, set
    member val preventCancel: bool option = jsNative with get, set
    member val signal: AbortSignal option = jsNative with get, set

/// The TransformStream interface of the Streams API represents a concrete implementation of the pipe chain transform stream concept.
///
/// It may be passed to the ReadableStream.pipeThrough() method in order to transform a stream of data from one format into another. For example, it might be used to decode (or encode) video frames, decompress data, or convert the stream from XML to JSON.
///
/// A transformation algorithm may be provided as an optional argument to the object constructor. If not supplied, data is not modified when piped through the stream.
///
/// https://developer.mozilla.org/en-US/docs/Web/API/TransformStream
///
/// Also known as ReadableWritablePair.
///
/// https://streams.spec.whatwg.org/#dictdef-readablewritablepair
type TransformStream<'T, 'U> =
  abstract writable : WritableStream<'T>
  abstract readable : ReadableStream<'U>

/// The WritableStream interface of the Streams API provides a standard abstraction for writing streaming data to a destination, known as a sink. This object comes with built-in backpressure and queuing.
///
/// https://developer.mozilla.org/en-US/docs/Web/API/WritableStream
///
/// https://streams.spec.whatwg.org/#ws-class
and WritableStream<'T> =
  interface end

/// The ReadableStream interface of the Streams API represents a readable stream of byte data. The Fetch API offers a concrete instance of a ReadableStream through the body property of a Response object.
///
/// https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream
/// 
/// https://streams.spec.whatwg.org/#rs-class

// TODO: async iterable support
// https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream#async_iteration_of_a_stream_using_for_await...of
// https://github.com/fable-compiler/fable-promise/pull/36/files
and ReadableStream<'T> =
  /// The cancel() method of the ReadableStreamDefaultReader interface returns a Promise that resolves when the stream is canceled. Calling this method signals a loss of interest in the stream by a consumer.
  ///
  /// Cancel is used when you've completely finished with the stream and don't need any more data from it, even if there are chunks enqueued waiting to be read. That data is lost after cancel is called, and the stream is not readable any more. To read those chunks still and not completely get rid of the stream, you'd use ReadableStreamDefaultController.close().
  ///
  /// Note: If the reader is active, the cancel() method behaves the same as that for the associated stream (ReadableStream.cancel()).
  ///
  /// https://developer.mozilla.org/en-US/docs/Web/API/ReadableStreamDefaultReader/cancel
  abstract cancel : ?reason:string -> Promise<unit>

  /// The getReader() method of the ReadableStream interface creates a reader and locks the stream to it. While the stream is locked, no other reader can be acquired until this one is released.
  ///
  /// https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream/getReader
  abstract getReader : unit -> ReadableStreamDefaultReader<'T>

  // getReader() returns a DefaultReader, getReader({mode:"byob"}) returns a BYOBReader
  // https://streams.spec.whatwg.org/#rs-prototype
  //abstract getReader : options -> ReadableStreamDefaultReader

  /// The pipeThrough() method of the ReadableStream interface provides a chainable way of piping the current stream through a transform stream or any other writable/readable pair.
  ///
  /// Piping a stream will generally lock it for the duration of the pipe, preventing other readers from locking it.
  ///
  /// https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream/pipeThrough
  abstract pipeThrough : transform : TransformStream<'T, 'U> * ?options : StreamPipeOptions -> ReadableStream<'U>

type Response with
  [<Emit ("$0.body")>]
  member _.body : ReadableStream<Uint8Array> = jsNative