namespace FSharp.Data.GraphQL.Samples.CollaborationHub.Shared

open System

type Counter = int

type Root =
    { ClientId : string }

type Status =
    | Online
    | Away
    | Busy
    | Offline

type User =
    { Nickname : string
      Name : string
      Status : Status }

type Channel =
    { Name : string
      Description : string
      mutable Members : User list }

type Destination =
    | User of User
    | Channel of Channel

type Message =
    { Contents : string
      Timestamp : DateTime
      Sender : User
      Destination : Destination }

[<AutoOpen>]
module Utils =
    let tee f x = f x; x