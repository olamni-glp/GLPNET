# Contract: The 043 Schema DSL (qmedit family)

Normative grammar for the authoring language (FR-001; research R4). Plaintext only — no XML
anywhere (clarification 1). Lexical family = the stored qmedit form: `{}` blocks, `name: type`
fields, `?` optional suffix, `[T]` arrays, `enum(...)`, `//` line comments.

## Grammar (EBNF)

```ebnf
document      = header , { named-type | message-decl } ;
header        = "schema" , ident , "version" , integer ;

named-type    = simple-type | complex-type ;
simple-type   = "type" , type-name , ":" , primitive , "{" , { facet } , "}" ;
complex-type  = "type" , type-name , "{" , composition , "}" ;
message-decl  = "message" , functor-name , "{" , composition , "}" ;

composition   = ( "sequence" | "choice" ) , "{" , element , { element } , "}" ;
element       = elem-name , ":" , type-ref , [ occurs ] ;
type-ref      = type-name | primitive | "[" , type-ref , "]" ;      (* [] = list, occurs on elements only *)
occurs        = "occurs" , integer , ".." , ( integer | "*" )
              | "?" ;                                               (* sugar for occurs 0..1, suffix on elem-name *)

primitive     = "int" | "str" | "bytes" | "bool" ;
                (* no standalone symbol primitive: symbolic constants are enum members on a
                   str base — matches the shipped qmedit `enum(none, state_based, op_based)`
                   idiom and keeps lift(lower) deterministic (str and symbol would both lower
                   to tstr and be indistinguishable on lift) *)

facet         = "min"        integer                                (* int only *)
              | "max"        integer                                (* int only *)
              | "minLength"  integer                                (* str, bytes *)
              | "maxLength"  integer                                (* str, bytes *)
              | "pattern"    string-literal                         (* str only; restricted subset, R6 *)
              | "enum"       "(" , literal , { "," , literal } , ")" ;  (* str, int *)

type-name     = UpperCamel ident ;      functor-name = lower_snake ident ;
elem-name     = lower_snake ident ;     literal      = string-literal | integer | ident ;
comment       = "//" to end-of-line ;
```

Defaults: omitted `occurs` = `1..1`. `elem?: T` ≡ `elem: T occurs 0..1` (matches the stored
qmedit `capability_slot?:` style). `occurs a..*` = unbounded max. `[T]` ≡ unbounded list of `T`
(matches stored `targets: [str]`).

## Example (the SC-006 walkthrough schema)

```text
schema chat version 1

type UserName: str { minLength 1  maxLength 64  pattern "[a-z][a-z0-9_]*" }
type Priority: int { min 0  max 9 }
type Attachment { sequence { name: UserName  size: int } }
type Body { choice { text: str  attachments: Attachment occurs 1..8 } }

message chat_message {
  sequence {
    from:     UserName
    priority: Priority occurs 0..1     // optional
    body:     Body
  }
}
```

## Well-formedness (FR-002 — checked at schema-validation, all with line:col location)

1. Type names unique per document; element names unique per composition; functor names unique
   per document.
2. Every `type-ref` resolves to a named type in this document or a primitive.
3. Facets consistent (data-model §1 table); facets only on legal bases.
4. The named-type reference graph is a **DAG**: any cycle, including self-reference, is rejected
   naming the full cycle path (`A → B → A`). Recursion is not supported (clarification 2).
5. `occurs` bounds: `0 ≤ min`, `min ≤ max` when max is finite.
6. A `choice` has ≥ 2 elements; a `sequence` has ≥ 1.

## Restricted pattern subset (R6)

Supported: literal chars, escapes `\.` `\\` `\-`, `.`, character classes `[a-z0-9_]` (ranges,
negation `[^…]`), grouping `( )`, alternation `|`, quantifiers `*` `+` `?` `{n}` `{n,m}`.
Implicitly anchored both ends. Anything else (backreferences, lookaround, named groups, flags)
⇒ schema-validation error naming the construct. Emptiness checked by NFA reachability;
an empty-language pattern is a facet-consistency error.
