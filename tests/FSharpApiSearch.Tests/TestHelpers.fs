﻿module TestHelpers
open FSharpApiSearch

let rec updateSource newSource = function
  | Variable (_, name) -> Variable (newSource, name)
  | StrongVariable (_, name) -> StrongVariable(newSource, name)
  | Identity _ as x -> x
  | StrongIdentity _ as x -> x
  | Wildcard as x -> x
  | WildcardGroup _ as x -> x
  | Arrow xs -> Arrow (List.map (updateSource newSource) xs)
  | Generic (id, xs) -> Generic (id, List.map (updateSource newSource) xs)
  | Tuple xs -> Tuple (List.map (updateSource newSource) xs)
  | StaticMethod x ->
    StaticMethod {
      Arguments = List.map (updateSource newSource) x.Arguments
      ReturnType = updateSource newSource x.ReturnType
    }
  | InstanceMember x ->
    InstanceMember {
      Source = newSource
      Receiver = updateSource newSource x.Receiver
      Arguments = List.map (updateSource newSource) x.Arguments
      ReturnType = updateSource newSource x.ReturnType
    }
  | Unknown as x -> x

let toStaticMethod = function
  | Arrow xs ->
    match xs with
    | [ Tuple arguments; returnType ] -> StaticMethod { Arguments = arguments; ReturnType = returnType }
    | [ argument; returnType ] -> StaticMethod { Arguments = [ argument ]; ReturnType = returnType }
    | _ -> Unknown
  | _ -> Unknown

module DSL =
  let private source = Source.Query
  let identity name = Identity name
  let strongIdentity name = StrongIdentity name
  let variable name = Variable (source, name)
  let strongVariable name = StrongVariable (source, name)
  let wildcard = Wildcard
  let wildcardGroup name = WildcardGroup name
  let arrow xs = Arrow xs
  let generic id xs = Generic (id, xs)
  let tuple xs = Tuple xs
  let staticMethod args ret = StaticMethod { Arguments = args; ReturnType = ret }
  let instanceMember receiver args ret = InstanceMember { Source = source; Receiver = receiver; Arguments = args; ReturnType = ret }
  let array x = generic (identity "[]") [ x ]
  let array2d x = generic (identity "[,]") [ x ]
  let array3d x = generic (identity "[,,]") [ x ]