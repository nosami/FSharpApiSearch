﻿module ApiLoaderTest

open System.IO
open System.Reflection
open Persimmon
open Persimmon.Syntax.UseTestNameByReflection
open FSharpApiSearch
open TestHelper.DSL
open TestAssemblies

let emptyDef: FullTypeDefinition = {
  Name = []
  AssemblyName = ""
  BaseType = None
  AllInterfaces = []
  GenericParameters = []
  TypeConstraints = []
  InstanceMembers = []
  StaticMembers = []

  ImplicitInstanceMembers = []
  ImplicitStaticMembers = []

  SupportNull = NotSatisfy
  ReferenceType = Satisfy
  ValueType = NotSatisfy
  DefaultConstructor = NotSatisfy
  Equality = Satisfy
  Comparison = NotSatisfy
}

let mscorlib = "mscorlib"
let fscore = "FSharp.Core"

let object' = createType "System.Object" [] |> updateAssembly mscorlib
let obj =
  let obj = createType "Microsoft.FSharp.Core.obj" [] |> updateAssembly fscore
  typeAbbreviation object' obj

let int32 = createType "System.Int32" [] |> updateAssembly mscorlib
let int =
  let int = createType "Microsoft.FSharp.Core.int" [] |> updateAssembly fscore
  typeAbbreviation int32 int

let double = createType "System.Double" [] |> updateAssembly mscorlib
let float =
  let float = createType "Microsoft.FSharp.Core.float" [] |> updateAssembly fscore
  typeAbbreviation double float

let unit =
  let Unit = createType "Microsoft.FSharp.Core.Unit" [] |> updateAssembly fscore
  let unit = createType "Microsoft.FSharp.Core.unit" [] |> updateAssembly fscore
  typeAbbreviation Unit unit

let ienumerable t = createType "System.Collections.Generic.IEnumerable" [ t ] |> updateAssembly mscorlib
let seq t =
  let seq = createType "Microsoft.FSharp.Collections.seq" [ t ] |> updateAssembly fscore
  typeAbbreviation (ienumerable t) seq

let fsharpList t = createType "Microsoft.FSharp.Collections.List" [ t] |> updateAssembly fscore
let list t =
  let list = createType "Microsoft.FSharp.Collections.list" [ t ] |> updateAssembly fscore
  typeAbbreviation (fsharpList t) list

let string =
  let String = createType "System.String" [] |> updateAssembly mscorlib
  let string = createType "Microsoft.FSharp.Core.string" [] |> updateAssembly fscore
  typeAbbreviation String string

let map k v = createType "Microsoft.FSharp.Collections.Map" [ k; v ] |> updateAssembly fscore

let array = array >> updateAssembly fscore
let array2D = array2D >> updateAssembly fscore

let istructualEquatable = createType "System.Collections.IStructuralEquatable" [] |> updateAssembly mscorlib
let iequatable x = createType "System.IEquatable" [ x ] |> updateAssembly mscorlib
let genericIComparable x = createType "System.IComparable" [ x ] |> updateAssembly mscorlib
let icomparable = createType "System.IComparable" [] |> updateAssembly mscorlib
let istructuralComparable = createType "System.IStructuralComparable" [] |> updateAssembly mscorlib

let valuetype = createType "System.ValueType" [] |> updateAssembly mscorlib

let testApi (assembly: TestCase<ApiDictionary>) (name, expected) = test {
  let! apiDict = assembly
  let name = ReverseName.ofString name
  let actual =
    Seq.filter (fun x -> x.Name = name) apiDict.Api 
    |> Seq.map (fun x -> x.Signature)
    |> Seq.filter (function (ApiSignature.FullTypeDefinition _ | ApiSignature.TypeAbbreviation _) -> false | _ -> true)
    |> Seq.toList
    |> List.sort
  let expected = expected |> List.sort
  do! actual |> assertEquals expected
}

let testFullTypeDef' (assembly: TestCase<ApiDictionary>) filter (name, expected) = test {
  let! apiDict = assembly
  let actual =
    Seq.filter (fun x -> x.Name = name) apiDict.Api
    |> Seq.map (fun x -> x.Signature)
    |> Seq.choose (function ApiSignature.FullTypeDefinition x -> Some x | _ -> None)
    |> Seq.head
  do! (filter actual) |> assertEquals expected
}

let testFullTypeDef (assembly: TestCase<ApiDictionary>) (expected: FullTypeDefinition) = testFullTypeDef' assembly id (expected.Name, expected)

let testConstraints (assembly: TestCase<ApiDictionary>) (name, expectedTarget, expectedConstraints) = test {
  let! apiDict = assembly
  let name = ReverseName.ofString name
  let actual = Seq.find (fun x -> x.Name = name) apiDict.Api
  do! actual.Signature |> assertEquals expectedTarget
  do! (List.sort actual.TypeConstraints) |> assertEquals (List.sort expectedConstraints)
}

module FSharp =
  let testApi = testApi fsharpAssemblyApi
  let testConstraints = testConstraints fsharpAssemblyApi

  let loadModuleMemberTest = parameterize {
    source [
      "PublicModule.nonGenericFunction", [ moduleFunction [ int; int; int ] ]
      "PublicModule.genericFunction", [ moduleFunction [ variable "a"; variable "b"; variable "b" ] ]
      "PublicModule.tupleFunction", [ moduleFunction [ tuple [ variable "a"; variable "b"; variable "c" ]; variable "a" ] ]
      "PublicModule.value", [ moduleValue int ]
      "PublicModule.NestedModule.publicFunction", [ moduleFunction [ int; int ] ]
      "PublicModule.listmap", [ moduleFunction [ arrow [ variable "a"; variable "b" ]; list (variable "a"); list (variable "b") ] ]
      "PublicModule.partialGenericMap", [ moduleFunction [ map int (variable "a"); variable "a" ] ]
      "PublicModule.floatReturnType", [ moduleFunction [ int; float ] ]
      "PublicModule.array", [ moduleValue (array int) ]
      "PublicModule.array2d", [ moduleValue (array2D int) ]
      "PublicModule.nestedArray", [ moduleValue (array (array2D int)) ]
    ]
    run testApi
  }

  let loadStaticMemberTest =
    let t = createType "TopLevelNamespace.StaticMemberClass" [] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "TopLevelNamespace.StaticMemberClass.NoArgumentMethod", [ staticMember t (method' "NoArgumentMethod" [ unit ] int) ]
        "TopLevelNamespace.StaticMemberClass.OneArgumentMethod", [ staticMember t (method' "OneArgumentMethod" [ int ] int) ]
        "TopLevelNamespace.StaticMemberClass.NonCurriedMethod", [ staticMember t (method' "NonCurriedMethod" [ int; string ] int) ]
        "TopLevelNamespace.StaticMemberClass.CurriedMethod", [ staticMember t (curriedMethod "CurriedMethod" [ int; string ] int) ]
        "TopLevelNamespace.StaticMemberClass.TupleMethod", [ staticMember t (method' "TupleMethod" [ tuple [ int; string ] ] int) ]
        "TopLevelNamespace.StaticMemberClass.InferredFloat", [ staticMember t (method' "InferredFloat" [ float ] float) ]
        "TopLevelNamespace.StaticMemberClass.AnnotatedFloat", [ staticMember t (method' "AnnotatedFloat" [ float ] float) ]
        "TopLevelNamespace.StaticMemberClass", [ constructor' t (method' "StaticMemberClass" [ unit ] t); constructor' t (method' "StaticMemberClass" [ int ] t) ]
        "TopLevelNamespace.StaticMemberClass.OverloadMethod", [ staticMember t (method' "OverloadMethod" [ int ] int); staticMember t (method' "OverloadMethod" [ string; int ] string) ]
        "TopLevelNamespace.StaticMemberClass.Getter", [ staticMember t (property' "Getter" PropertyKind.Get [] string) ]
        "TopLevelNamespace.StaticMemberClass.Setter", [ staticMember t (property' "Setter" PropertyKind.Set [] int) ]
        "TopLevelNamespace.StaticMemberClass.GetterSetter", [ staticMember t (property' "GetterSetter" PropertyKind.GetSet [] float) ]
        "TopLevelNamespace.StaticMemberClass.IndexedGetter", [ staticMember t (property' "IndexedGetter" PropertyKind.Get [ int ] string) ]
        "TopLevelNamespace.StaticMemberClass.IndexedSetter", [ staticMember t (property' "IndexedSetter" PropertyKind.Set [ int ] string) ]
        "TopLevelNamespace.StaticMemberClass.IndexedGetterSetter", [ staticMember t (property' "IndexedGetterSetter" PropertyKind.GetSet [ string ] int) ]
      ]
      run testApi
    }

  let loadInstanceMemberTest =
    let t = createType "TopLevelNamespace.InstanceMemberClass" [] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "TopLevelNamespace.InstanceMemberClass.NoArgumentMethod", [ instanceMember t (method' "NoArgumentMethod" [ unit ] int) ]
        "TopLevelNamespace.InstanceMemberClass.OneArgumentMethod", [ instanceMember t (method' "OneArgumentMethod" [ int ] int) ]
        "TopLevelNamespace.InstanceMemberClass.NonCurriedMethod", [ instanceMember t (method' "NonCurriedMethod" [ int; string ] int) ]
        "TopLevelNamespace.InstanceMemberClass.CurriedMethod", [ instanceMember t (curriedMethod "CurriedMethod" [ int; string ] int) ]
        "TopLevelNamespace.InstanceMemberClass.TupleMethod", [ instanceMember t (method' "TupleMethod" [ tuple [ int; string ] ] int) ]
        "TopLevelNamespace.InstanceMemberClass.OverloadMethod", [ instanceMember t (method' "OverloadMethod" [ int ] int); instanceMember t (method' "OverloadMethod" [ string; int ] string) ]
        "TopLevelNamespace.InstanceMemberClass.Getter", [ instanceMember t (property' "Getter" PropertyKind.Get [] string) ]
        "TopLevelNamespace.InstanceMemberClass.Setter", [ instanceMember t (property' "Setter" PropertyKind.Set [] int) ]
        "TopLevelNamespace.InstanceMemberClass.GetterSetter", [ instanceMember t (property' "GetterSetter" PropertyKind.GetSet [] float) ]
        "TopLevelNamespace.InstanceMemberClass.IndexedGetter", [ instanceMember t (property' "IndexedGetter" PropertyKind.Get [ int ] string) ]
        "TopLevelNamespace.InstanceMemberClass.IndexedSetter", [ instanceMember t (property' "IndexedSetter" PropertyKind.Set [ int ] string) ]
        "TopLevelNamespace.InstanceMemberClass.IndexedGetterSetter", [ instanceMember t (property' "IndexedGetterSetter" PropertyKind.GetSet [ string ] int) ]
      ]
      run testApi
    }

  let loadGenericClassTest =
    let t = createType "TopLevelNamespace.GenericClass" [ variable "a" ] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "TopLevelNamespace.GenericClass.Method", [ instanceMember t (method' "Method" [ variable "a" ] int) ]
        "TopLevelNamespace.GenericClass", [ constructor' t (method' "GenericClass" [ unit ] t) ]
      ]
      run testApi
    }

  let loadRecordTest =
    let t = createType "OtherTypes.Record" [] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "OtherTypes.Record.FieldA", [ instanceMember t (field "FieldA" int) ]
        "OtherTypes.Record.FieldB", [ instanceMember t (field "FieldB" string) ]
        "OtherTypes.Record.InstanceMethod", [ instanceMember t (method' "InstanceMethod" [ unit ] int) ]
        "OtherTypes.Record.InstanceProperty", [ instanceMember t (property' "InstanceProperty" PropertyKind.GetSet [] int) ]
        "OtherTypes.Record.StaticMethod", [ staticMember t (method' "StaticMethod" [ unit ] string) ]
      ]
      run testApi  
    }

  let loadGenericRecordTest =
    let t = createType "OtherTypes.GenericRecord" [ variable "a" ] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "OtherTypes.GenericRecord.Field", [ instanceMember t (field "Field" (variable "a")) ]
      ]
      run testApi  
    }

  let loadUnionTest =
    let t = createType "OtherTypes.Union" [] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "OtherTypes.Union.InstanceMethod", [ instanceMember t (method' "InstanceMethod" [ unit ] int) ]
      ]
      run testApi
    }

  let laodStructTest =
    let t = createType "OtherTypes.Struct" [] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "OtherTypes.Struct.A", [ instanceMember t (field "A" int) ]
        "OtherTypes.Struct.B", [ instanceMember t (field "B" string) ]
        "OtherTypes.Struct.InstanceMethod", [ instanceMember t (method' "InstanceMethod" [ unit ] int) ]
      ]
      run testApi
    }

  let laodEnumTest =
    let t = createType "OtherTypes.Enum" [] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "OtherTypes.Enum.A", [ staticMember t (field "A" t) ]
        "OtherTypes.Enum.B", [ staticMember t (field "B" t) ]
      ]
      run testApi
    }

  let loadInterfaceTest =
    let t = createType "TopLevelNamespace.Interface" [] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "TopLevelNamespace.Interface.Method", [ instanceMember t (method' "Method" [ int; string] int ) ]
        "TopLevelNamespace.Interface.Property", [ instanceMember t (property' "Property" PropertyKind.GetSet [] string ) ]
      ]
      run testApi
    }

  let interfaceInheritanceTest =
    let child = createType "InterfaceInheritance.ChildInterface" [] |> updateAssembly fsharpAssemblyName
    let genericChild = createType "InterfaceInheritance.GenericChildInterface" [ variable "a" ] |> updateAssembly fsharpAssemblyName
    let intChild = createType "InterfaceInheritance.IntChildInterface" [] |> updateAssembly fsharpAssemblyName
    let confrict = createType "InterfaceInheritance.ConflictParameterInterface" [ variable "b" ] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        "InterfaceInheritance.ChildInterface.ChildMethod", [ instanceMember child (method' "ChildMethod" [ unit ] float) ]
        "InterfaceInheritance.ChildInterface.ParentMethod", [ instanceMember child (method' "ParentMethod" [ unit] string) ]
        "InterfaceInheritance.ChildInterface.GrandParentMethod", [ instanceMember child (method' "GrandParentMethod" [ unit ] int) ]
        "InterfaceInheritance.GenericChildInterface.ParentMethod", [ instanceMember genericChild (method' "ParentMethod" [ variable "a" ] (variable "b")) ]
        "InterfaceInheritance.GenericChildInterface.GrandParentMethod", [ instanceMember genericChild (method' "GrandParentMethod" [ variable "a" ] (variable "u")) ]
        "InterfaceInheritance.ConflictParameterInterface.ParentMethod", [ instanceMember confrict (method' "ParentMethod" [ variable "b" ] (variable "b1")) ]
        "InterfaceInheritance.IntChildInterface.ParentMethod", [ instanceMember intChild (method' "ParentMethod" [ int ] (variable "b")) ]
      ]
      run testApi
    }

  // bug #60
  let internalInterfaceTest = test {
    let! mscorDict = mscorlibApi
    let tuple = mscorDict.TypeDefinitions |> Array.find (fun x -> x.Name = ReverseName.ofString "System.Tuple" && x.GenericParameters.Length = 2)
    let existsITuple = tuple.AllInterfaces |> Seq.exists (function Identity (FullIdentity i) -> i.Name = ReverseName.ofString "System.ITuple" | _ -> false)
    do! existsITuple |> assertEquals false
  }

  let nonloadedTest =
    parameterize {
      source[
        "PublicModule.internalFunction"
        "PublicModule.privateFunction"
        "InternalModule.publicFunction"
        "PrivateModule.publicFunction"
        "OtherTypes.Enum.value__"
        "TopLevelNamespace.StaticMemberClass.PrivateMethod"
        "TopLevelNamespace.StaticMemberClass.InternalMethod"
        "TopLevelNamespace.PrivateClass.PublicMethod"
        "TopLevelNamespace.InternalClass.PublicMethod"
      ]
      run (fun x -> testApi (x, []))
    }
  let typeConstraintsTest =
    let subtypeClass = createType "TypeConstraints.SubTypeClass" [ variable "a" ] |> updateAssembly fsharpAssemblyName
    let subtypeRecord = createType "TypeConstraints.SubTypeRecord" [ variable "a" ] |> updateAssembly fsharpAssemblyName
    parameterize {
      source [
        // subtype
        ("TypeConstraints.subtypeConFunction",
          (moduleFunction [ variable "Tseq"; unit ]),
          [ constraint' [ "Tseq"] (SubtypeConstraints (seq int)) ])
        ("TypeConstraints.SubTypeClass.Method",
          (staticMember subtypeClass (method' "Method" [ variable "a"; variable "b" ] unit)),
          [ constraint' [ "a" ] (SubtypeConstraints (seq int)); constraint' [ "b" ] (SubtypeConstraints (seq string)) ])
        ("TypeConstraints.SubTypeRecord.Field",
          (instanceMember subtypeRecord (field "Field" (variable "a"))),
          [ constraint' [ "a" ] (SubtypeConstraints (seq int)) ])

        // nullness
        ("TypeConstraints.nullnessFunction",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] NullnessConstraints ])

        // member
        ("TypeConstraints.memberConstraint_instanceMethod1",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "Method" MemberKind.Method [ int; int ] int)) ])
        ("TypeConstraints.memberConstraint_instanceMethod2",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "Method" MemberKind.Method [ int; int ] int)) ])
        ("TypeConstraints.memberConstraint_tupleMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "Method" MemberKind.Method [ tuple [ int; int ] ] int)) ])
        ("TypeConstraints.memberConstraint_staticMember",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Static, member' "Method" MemberKind.Method [ int ] int)) ])
        ("TypeConstraints.memberConstraint_or",
          (moduleFunction [ variable "a"; variable "b"; unit ]),
          [ constraint' [ "a"; "b" ] (MemberConstraints (MemberModifier.Static, member' "Method" MemberKind.Method [ int ] int)) ])
        ("TypeConstraints.memberConstraint_noArgumentMember", // no argument means get property
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "get_Method" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_unitMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "Method" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_unitIntMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "Method" MemberKind.Method [ unit; int ] int)) ])
        ("TypeConstraints.memberConstraint_getterMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "get_Property" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_setterMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "set_Property" MemberKind.Method [ int ] unit)) ])
        ("TypeConstraints.memberConstraint_getProperty",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "get_Property" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_setProperty",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "set_Property" MemberKind.Method [ int ] unit)) ])
        ("TypeConstraints.memberConstraint_indexedGetProperty",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "get_Property" MemberKind.Method [ int ] int)) ])
        ("TypeConstraints.memberConstraint_indexedSetProperty",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "set_Property" MemberKind.Method [ int; int ] unit)) ])
        ("TypeConstraints.memberConstraint_staticNoArgumentMember", // no argument means get property
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Static, member' "get_Method" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_staticUnitMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Static, member' "Method" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_staticGetterMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Static, member' "get_Property" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_staticSetterMethod",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Static, member' "set_Property" MemberKind.Method [ int ] unit)) ])
        ("TypeConstraints.memberConstraint_staticGetProperty",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Static, member' "get_Property" MemberKind.Method [ unit ] int)) ])
        ("TypeConstraints.memberConstraint_staticSetProperty",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Static, member' "set_Property" MemberKind.Method [ int ] unit)) ])
        ("TypeConstraints.memberConstraint_generic",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"] (MemberConstraints (MemberModifier.Instance, member' "Method" MemberKind.Method [ variable "b" ] unit)) ])
        ("TypeConstraints.memberConstraint_operator",
          (moduleFunction [ variable "a"; variable "b"; unit ]),
          [ constraint' [ "a"; "b"; ] (MemberConstraints (MemberModifier.Static, member' "op_Addition" MemberKind.Method [ variable "a"; variable "b" ] (variable "c"))) ])

        // value, reference
        ("TypeConstraints.valueTypeConstraint",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"; ] ValueTypeConstraints ])
        ("TypeConstraints.referenceTypeConstraint",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"; ] ReferenceTypeConstraints ])

        // default constructor
        ("TypeConstraints.defaultConstructorConstraint",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"; ] DefaultConstructorConstraints ])

        // equality
        ("TypeConstraints.equalityConstraint",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"; ] EqualityConstraints ])

        // comparison
        ("TypeConstraints.comparisonConstraint",
          (moduleFunction [ variable "a"; unit ]),
          [ constraint' [ "a"; ] ComparisonConstraints ])
      ]
      run testConstraints
    }

  let fullTypeDefinitionTest =
    let plainClass = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.PlainClass"
        AssemblyName = fsharpAssemblyName
        BaseType = Some obj
        DefaultConstructor = Satisfy
    }

    let plainInterface = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.PlainInterface"
        AssemblyName = fsharpAssemblyName
    }

    let interfaceImplClass = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.InterfaceImplClass"
        AssemblyName = fsharpAssemblyName
        BaseType = Some obj
        AllInterfaces = [ Identity (FullIdentity plainInterface.FullIdentity) ]
        DefaultConstructor = Satisfy
    }

    let interfaceInherit = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.InterfaceInherit"
        AssemblyName = fsharpAssemblyName
        AllInterfaces = [ Identity (FullIdentity plainInterface.FullIdentity) ]
    }

    let supportNullClass = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.SupportNullClass"
        AssemblyName = fsharpAssemblyName
        BaseType = Some obj
        SupportNull = Satisfy
        DefaultConstructor = Satisfy
    }

    let nonSupportNullSubClass = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.SupportNullSubClass"
        AssemblyName = fsharpAssemblyName
        BaseType = Some (Identity (FullIdentity supportNullClass.FullIdentity))
        SupportNull = NotSatisfy
        DefaultConstructor = Satisfy
    }

    let supportNullInterface = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.SupportNullInterface"
        AssemblyName = fsharpAssemblyName
        SupportNull = Satisfy
    }

    let supportNullSubInterface = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.SupportNullSubInterface"
        AssemblyName = fsharpAssemblyName
        AllInterfaces = [ Identity (FullIdentity supportNullInterface.FullIdentity) ]
        SupportNull = Satisfy
    }

    let nonSupportNullSubInterface = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.NonSupportNullSubInterface"
        AssemblyName = fsharpAssemblyName
        AllInterfaces = [ Identity (FullIdentity supportNullInterface.FullIdentity) ]
        SupportNull = NotSatisfy
    }

    let withoutDefaultConstructor = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.WithoutDefaultConstructor"
        AssemblyName = fsharpAssemblyName
        BaseType = Some obj
        DefaultConstructor = NotSatisfy
    }

    let memberClassId = createType "FullTypeDefinition.MemberClass" [] |> updateAssembly fsharpAssemblyName

    let memberClass = {
      emptyDef with
        Name = ReverseName.ofString "FullTypeDefinition.MemberClass"
        AssemblyName = fsharpAssemblyName
        BaseType = Some obj
        StaticMembers =
          [
            method' "StaticMethod" [ unit ] int
            method' "op_Addition" [ memberClassId; int ] memberClassId
          ]
        InstanceMembers =
          [
            method' "InstanceMethod" [ int ] int
            property' "Property" PropertyKind.Get [] int
          ]
        DefaultConstructor = Satisfy
    }

    parameterize {
      source [
        plainClass
        plainInterface
        interfaceImplClass
        interfaceInherit

        supportNullClass
        nonSupportNullSubClass
        supportNullInterface
        supportNullSubInterface
        nonSupportNullSubInterface

        memberClass

        withoutDefaultConstructor
      ]
      run (testFullTypeDef fsharpAssemblyApi)
    }

  let testEquality = parameterize {
    source [
      "EqualityType", Satisfy
      "NoEqualityType", NotSatisfy
      "InferredEqualityRecord", Satisfy
      "InferredNoEqualityRecord", NotSatisfy
      "InferredEqualityUnion", Satisfy
      "InferredNoEqualityUnion", NotSatisfy
      "CustomEqualityRecord", Satisfy
      "GenericClass", Satisfy
      "EqualityConditionalClass", Dependence [ "a" ]
      "CustomEqualityAndConditionalRecord", Dependence [ "a" ]
      "EqualityGenericRecord", Dependence [ "a"; "b" ]
      "NoEqualityGenericRecord", NotSatisfy
      "EqualityWithGenericType", Satisfy
      "NoEqualityWithGenericType", NotSatisfy
      "RecursiveType", Dependence [ "a" ]
    ]
    run (fun (name, expected) ->
      testFullTypeDef' fsharpAssemblyApi (fun x -> x.Equality) (name :: ReverseName.ofString "FullTypeDefinition.EqualityConstraints", expected))
  }

  let testComparison = parameterize {
    source [
      "ComparisonType", Satisfy
      "NotComparisonType", NotSatisfy
      "StructualComparisonType", Satisfy
      "InferredComparisonRecord", Satisfy
      "InferredNoComparisonRecord", NotSatisfy
      "NoComparisonRecord", NotSatisfy
      "InferredComparisonUnion", Satisfy
      "InferredNoComparisonUnion", NotSatisfy
      "CustomComparisonRecord", Satisfy
      "GenericNoComparisonClass", NotSatisfy
      "GenericComparisonClass", Satisfy
      "ComparisonConditionalClass", Dependence [ "a" ]
      "CustomComparisonAndConditionalRecord", Dependence [ "a" ]
      "ComparisonGenericRecord", Dependence [ "a"; "b" ]
      "NoComparisonGenericRecord", NotSatisfy
      "ComparisonWithGenericType", Satisfy
      "NoComparisonWithGenericType", NotSatisfy
      "RecursiveType", Dependence [ "a" ]
    ]
    run (fun (name, expected) ->
      testFullTypeDef' fsharpAssemblyApi (fun x -> x.Comparison) (name :: ReverseName.ofString "FullTypeDefinition.ComparisonConstraints", expected))
  }

  let compilerInternalTest = test {
    let! fscoreDict = fscoreApi
    let actual =
      fscoreDict.Api
      |> Seq.filter (fun x ->
        let name = ReverseName.toString x.Name
        name.StartsWith("Microsoft.FSharp.Core.LanguagePrimitives.") || name.StartsWith("Microsoft.FSharp.Core.Operators.OperatorIntrinsics.")
      )
      |> Seq.length
    do! actual |> assertEquals 0
  }

module SpecialType =
  let tupleName = ReverseName.ofString "System.Tuple"
  let tupleNullnessTest =
    testFullTypeDef' mscorlibApi (fun x -> x.SupportNull) (tupleName, NotSatisfy)
  let tupleEqualityTest =
    testFullTypeDef' mscorlibApi (fun x -> x.Equality) (tupleName, Dependence [ "T1"; "T2"; "T3"; "T4"; "T5"; "T6"; "T7"; "TRest"])
  let tupleComparisonTest =
    testFullTypeDef' mscorlibApi (fun x -> x.Comparison) (tupleName, Dependence [ "T1"; "T2"; "T3"; "T4"; "T5"; "T6"; "T7"; "TRest"])

  let arrayName = ReverseName.ofString "Microsoft.FSharp.Core.[]"

  let arrayNullnessTest =
    testFullTypeDef' fscoreApi (fun x -> x.SupportNull) (arrayName, Satisfy)
  let arrayEquality =
    testFullTypeDef' fscoreApi (fun x -> x.Equality) (arrayName, Dependence [ "T" ])
  let arrayComparison =
    testFullTypeDef' fscoreApi (fun x -> x.Comparison) (arrayName, Dependence [ "T" ])

  let intptrComparison =
    testFullTypeDef' mscorlibApi (fun x -> x.Comparison) (ReverseName.ofString "System.IntPtr", Satisfy)
  let uintptrComparison =
    testFullTypeDef' mscorlibApi (fun x -> x.Comparison) (ReverseName.ofString "System.UIntPtr", Satisfy)

  let int32ImplicitStaticMembers =
    testFullTypeDef' mscorlibApi (fun x -> x.ImplicitStaticMembers |> List.exists (fun x -> x.Name = "op_Addition")) (ReverseName.ofString "System.Int32", true)

module TypeAbbreviation =
  let A = createType "TypeAbbreviations.A" [] |> updateAssembly fsharpAssemblyName
  let typeAbbreviationTest = parameterize {
    source [
      { Abbreviation = createType "TypeAbbreviations.GenericTypeAbbreviation" [ variable "b" ] |> updateAssembly fsharpAssemblyName
        Original = createType "TypeAbbreviations.Original" [ variable "b" ] |> updateAssembly fsharpAssemblyName }
      { Abbreviation = createType "TypeAbbreviations.SpecializedTypeAbbreviation" [] |> updateAssembly fsharpAssemblyName
        Original = createType "TypeAbbreviations.Original" [ A ] |> updateAssembly fsharpAssemblyName }
      { Abbreviation = createType "TypeAbbreviations.NestedTypeAbbreviation" [] |> updateAssembly fsharpAssemblyName
        Original = createType "TypeAbbreviations.Original"[ A ]  |> updateAssembly fsharpAssemblyName}
      { Abbreviation = createType "TypeAbbreviations.NestedModule.TypeAbbreviationInModule" [ variable "a" ] |> updateAssembly fsharpAssemblyName
        Original = createType "TypeAbbreviations.Original" [ variable "a" ] |> updateAssembly fsharpAssemblyName }
    ]
    run (fun entry -> test {
      let! api = fsharpAssemblyApi
      let actual = api.TypeAbbreviations |> Seq.contains entry
      do! actual |> assertEquals true
    })
  }

module CSharp =
  let testApi = testApi csharpAssemblyApi
  let testConstraints = testConstraints csharpAssemblyApi

  let loadStaticMemberTest =
    let t = createType "CSharpLoadTestAssembly.StaticMemberClass" [] |> updateAssembly csharpAssemblyName
    parameterize {
      source [
        "CSharpLoadTestAssembly.StaticMemberClass.NoArgumentMethod", [ staticMember t (method' "NoArgumentMethod" [ unit ] int) ]
        "CSharpLoadTestAssembly.StaticMemberClass.NonCurriedMethod", [ staticMember t (method' "NonCurriedMethod" [ int; string ] unit) ]
        "CSharpLoadTestAssembly.StaticMemberClass.TupleMethod", [ staticMember t (method' "TupleMethod" [ tuple [ int; string ] ] unit) ]
        "CSharpLoadTestAssembly.StaticMemberClass", [ constructor' t (method' "StaticMemberClass" [ unit ] t); constructor' t (method' "StaticMemberClass" [ string; string ] t) ]
        "CSharpLoadTestAssembly.StaticMemberClass.OverloadMethod", [ staticMember t (method' "OverloadMethod" [ int ] int); staticMember t (method' "OverloadMethod" [ string ] string) ]
        "CSharpLoadTestAssembly.StaticMemberClass.Getter", [ staticMember t (property' "Getter" PropertyKind.Get [] string) ]
        "CSharpLoadTestAssembly.StaticMemberClass.Setter", [ staticMember t (property' "Setter" PropertyKind.Set [] string) ]
        "CSharpLoadTestAssembly.StaticMemberClass.GetterSetter", [ staticMember t (property' "GetterSetter" PropertyKind.GetSet [] string) ]
      ]
      run testApi
    }

  let loadArrayTest =
    let t = createType "CSharpLoadTestAssembly.StaticMemberClass" [] |> updateAssembly csharpAssemblyName
    parameterize {
      source [
        "CSharpLoadTestAssembly.StaticMemberClass.ArrayMethod", [ staticMember t (method' "ArrayMethod" [ unit ] (array int)) ]
        "CSharpLoadTestAssembly.StaticMemberClass.Array2dMethod", [ staticMember t (method' "Array2dMethod" [ unit ] (array2D int)) ]
        "CSharpLoadTestAssembly.StaticMemberClass.NestedArrayMethod", [ staticMember t (method' "NestedArrayMethod" [ unit ] (array2D (array int))) ] // defined as int[,][] in C#
      ]
      run testApi
    }

  let loadInstanceMemberTest =
    let t = createType "CSharpLoadTestAssembly.InstanceMemberClass" [] |> updateAssembly csharpAssemblyName
    parameterize {
      source [
        "CSharpLoadTestAssembly.InstanceMemberClass.NoArgumentMethod", [ instanceMember t (method' "NoArgumentMethod" [ unit ] int) ]
        "CSharpLoadTestAssembly.InstanceMemberClass.NonCurriedMethod", [ instanceMember t (method' "NonCurriedMethod" [ int; string ] unit) ]
        "CSharpLoadTestAssembly.InstanceMemberClass.TupleMethod", [ instanceMember t (method' "TupleMethod" [ tuple [ int; string ] ] unit) ]
        "CSharpLoadTestAssembly.InstanceMemberClass.OverloadMethod", [ instanceMember t (method' "OverloadMethod" [ int ] int); instanceMember t (method' "OverloadMethod" [ string ] string) ]
        "CSharpLoadTestAssembly.InstanceMemberClass.Getter", [ instanceMember t (property' "Getter" PropertyKind.Get [] string) ]
        "CSharpLoadTestAssembly.InstanceMemberClass.Setter", [ instanceMember t (property' "Setter" PropertyKind.Set [] string) ]
        "CSharpLoadTestAssembly.InstanceMemberClass.GetterSetter", [ instanceMember t (property' "GetterSetter" PropertyKind.GetSet [] string) ]
      ]
      run testApi
    }

  let loadIndexerTest =
    let getter = createType "CSharpLoadTestAssembly.IndexedGetter" [] |> updateAssembly csharpAssemblyName
    let setter = createType "CSharpLoadTestAssembly.IndexedSetter" [] |> updateAssembly csharpAssemblyName
    let gettersetter = createType "CSharpLoadTestAssembly.IndexedGetterSetter" [] |> updateAssembly csharpAssemblyName
    parameterize {
      source [
        "CSharpLoadTestAssembly.IndexedGetter.Item", [ instanceMember getter (property' "Item" PropertyKind.Get [ int ] int) ]
        "CSharpLoadTestAssembly.IndexedSetter.Item", [ instanceMember setter (property' "Item" PropertyKind.Set [ int ] int) ]
        "CSharpLoadTestAssembly.IndexedGetterSetter.Item", [ instanceMember gettersetter (property' "Item" PropertyKind.GetSet [ int ] int) ]
      ]
      run testApi
    }

  let loadInnerClassTest =
    let outer = createType "CSharpLoadTestAssembly.OuterClass" [] |> updateAssembly csharpAssemblyName
    let inner = createType "CSharpLoadTestAssembly.OuterClass.InnerClass" [] |> updateAssembly csharpAssemblyName
    parameterize {
      source [
        "CSharpLoadTestAssembly.OuterClass.InnerClass", [ constructor' inner (method' "InnerClass" [ unit ] inner) ]
        "CSharpLoadTestAssembly.OuterClass.InnerClass.StaticMethod", [ staticMember inner (method' "StaticMethod" [ unit ] int) ]
      ]
      run testApi
    }

  let loadInterfaceTest =
    let i = createType "CSharpLoadTestAssembly.Interface" [] |> updateAssembly csharpAssemblyName
    let gi = createType "CSharpLoadTestAssembly.GenericInterface" [ variable "T" ] |> updateAssembly csharpAssemblyName
    parameterize {
      source [
        "CSharpLoadTestAssembly.Interface.Method", [ instanceMember i (method' "Method" [ int; string ] int) ]
        "CSharpLoadTestAssembly.Interface.Property", [ instanceMember i (property' "Property" PropertyKind.GetSet [] int) ]
        "CSharpLoadTestAssembly.GenericInterface.Method", [ instanceMember gi (method' "Method" [ variable "T" ] int) ]
        "CSharpLoadTestAssembly.GenericInterface.Property", [ instanceMember gi (property' "Property" PropertyKind.Set [] (variable "T")) ]
      ]
      run testApi
    }

  let nonloadedTest =
    parameterize {
      source[
        "CSharpLoadTestAssembly.StaticMemberClass.Field"
        "CSharpLoadTestAssembly.InstanceMemberClass.Field"
        "CSharpLoadTestAssembly.InstanceMemberClass.ProtectedMethod"
        "CSharpLoadTestAssembly.Struct.Field"
      ]
      run (fun x -> testApi (x, []))
    }

  let constraintsTest =
    let t = createType "CSharpLoadTestAssembly.TypeConstraints" [] |> updateAssembly csharpAssemblyName
    parameterize {
      source[
        ("CSharpLoadTestAssembly.TypeConstraints.Struct",
          (staticMember t (method' "Struct" [ variable "T" ] unit)),
          [ constraint' [ "T" ] (SubtypeConstraints valuetype); constraint' [ "T" ] DefaultConstructorConstraints; constraint' [ "T" ] ValueTypeConstraints ])
        ("CSharpLoadTestAssembly.TypeConstraints.Class",
          (staticMember t (method' "Class" [ variable "T" ] unit)),
          [ constraint' [ "T" ] ReferenceTypeConstraints ])
        ("CSharpLoadTestAssembly.TypeConstraints.New",
          (staticMember t (method' "New" [ variable "T" ] unit)),
          [ constraint' [ "T" ] DefaultConstructorConstraints ])
        ("CSharpLoadTestAssembly.TypeConstraints.Subtype",
          (staticMember t (method' "Subtype" [ variable "T" ] unit)),
          [ constraint' [ "T" ] (SubtypeConstraints icomparable) ])
        ("CSharpLoadTestAssembly.TypeConstraints.VariableSubtype",
          (staticMember t (method' "VariableSubtype" [ variable "T"; variable "U" ] unit)),
          [ constraint' [ "T" ] (SubtypeConstraints (variable "U")) ])
      ]
      run testConstraints
    }