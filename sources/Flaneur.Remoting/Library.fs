namespace Flaneur.Remoting

type Encoder<'Encoded> = System.Type -> obj -> 'Encoded
type Decoder<'Encoded> = System.Type -> 'Encoded -> obj
