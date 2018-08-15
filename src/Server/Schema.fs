module FSharp.Data.GraphQL.Samples.CollaborationHub.Server.Schema

open FSharp.Data.GraphQL.Types
open System.Collections.Concurrent
open FSharp.Data.GraphQL.Samples.CollaborationHub.Shared

#nowarn "40"

let private users = ConcurrentBag<User>()

let private channels = ConcurrentBag<Channel>()

let private messages = ConcurrentBag<Message>()

let private equals (x : string) (y : string) =
    System.String.Equals(x, y, System.StringComparison.InvariantCultureIgnoreCase)

let getUser (nickname : string) =
    users |> Seq.tryFind (fun x -> x.Nickname |> equals nickname)

let getChannels (user : User) : Channel seq =
    channels
    |> Seq.filter (fun x -> x.Members |> Seq.contains user)
    |> Seq.sortBy (fun x -> x.Name)

let getChannel (name : string) =
    channels |> Seq.tryFind (fun x -> x.Name |> equals name)

let getMessages (destination : Destination) =
    messages
    |> Seq.filter (fun x -> x.Destination = destination)
    |> Seq.sortByDescending (fun x -> x.Timestamp)
    |> Seq.take 20
    |> Seq.rev

let StatusType =
    Define.Enum(
        name = "Status",
        description = "Defines the status of an user on the system.",
        options = [
            Define.EnumValue("Online", Status.Online,
                "The user is online, and will be notified about incoming messages ASAP.")
            Define.EnumValue("Away", Status.Away,
                "The user is online, but seems to be away from the keyboard. Will be notified ASAP, but may not see the messages soon.")
            Define.EnumValue("Busy", Status.Busy,
                "The user is online, but seems to be busy. Will receive messages ASAP, but will not be notified.")
            Define.EnumValue("Offline", Status.Offline,
                "The user is not connected. Will receive messages and be notified when becoming online.") ])

let UserType =
    Define.Object<User>(
        name = "User",
        description = "A user of the Collaboration Hub.",
        isTypeOf = (fun x -> x :? User),
        fieldsFn = fun () ->
            [ Define.Field("nickname", String, "The nickname of the user. Must be unique in the server.", fun _ x -> x.Nickname)
              Define.Field("name", String, "The full name of the user.", fun _ (x : User) -> x.Name)
              Define.Field("status", StatusType, "The current status of the user on the server.", fun _ x -> x.Status) ])

let ChannelType =
    Define.Object<Channel>(
        name = "Channel",
        description = "A channel defines a place where a group of users can collaborate together.",
        isTypeOf = (fun x -> x :? Channel),
        fieldsFn = fun () ->
            [ Define.Field("name", String,
                "The name of the channel. Must be unique on the server.", fun _ x -> x.Name)
              Define.Field("description", String,
                "A short description about the channel purpose on the server.", fun _ x -> x.Description)
              Define.Field("members", ListOf UserType,
                "The members of the channel are the ones that are collaborating together on it.", fun _ x -> x.Members) ])

let DestinationType =
    Define.Union(
        name = "Destination",
        description = "The destination of a message sent by an user.",
        options = [ UserType; ChannelType ],
        resolveValue = (fun x ->
            match x with
            | User u -> box u
            | Channel c -> upcast c),
        resolveType = (fun x ->
            match x with
            | User _ -> upcast UserType
            | Channel _ -> upcast ChannelType))

let MessageType =
    Define.Object<Message>(
        name = "Message",
        description = "A message sent by an user to another user or channel.",
        isTypeOf = (fun x -> x :? Message),
        fieldsFn = fun () ->
            [ Define.Field("contents", String,
                "Content of the message as a string.", fun _ x -> x.Contents)
              Define.Field("timestamp", Date,
                "The date and time when the message was sent.", fun _ x -> x.Timestamp)
              Define.Field("sender", UserType,
                "The user who sent the message.", fun _ x -> x.Sender)
              Define.Field("destination", DestinationType,
                "The destination of the message. Can be a user or a channel.", fun _ x -> x.Destination) ])

let Query =
    Define.Object<Root>(
        name = "Query",
        fields =
            [ Define.Field("user", Nullable UserType,
                "Gets information about an user.", [ Define.Input("nickname", String) ],
                fun ctx _ -> ctx.Arg("nickname") |> getUser)
              Define.Field("channels", ListOf ChannelType,
                "Gets the list of channels that the user is a member of.", [ Define.Input("nickname", String) ],
                fun ctx _ -> ctx.Arg("nickname") |> getUser |> Option.map getChannels |> Option.defaultValue Seq.empty) ])

let private createChannel (ctx : ResolveFieldContext) _ =
    let channel =
        { Name = ctx.Arg("name")
          Description = ctx.Arg("description")
          Members = ctx.Arg("members") |> Seq.map getUser |> Seq.choose id |> List.ofSeq }
    tee channels.Add channel

let private addUserToChannel (ctx : ResolveFieldContext) _ =
    let channel = ctx.Arg("channel") |> getChannel
    let user = ctx.Arg("user") |> getUser
    match channel, user with
    | Some c, Some u -> c.Members <- u :: c.Members; Some c
    | _ -> None

let private removeUserFromChannel (ctx : ResolveFieldContext) _ =
    let channel = ctx.Arg("channel") |> getChannel
    let user = ctx.Arg("user") |> getUser
    match channel, user with
    | Some c, Some u -> c.Members <- c.Members |> Seq.filter (fun x -> x.Nickname <> u.Nickname) |> List.ofSeq; Some c
    | _ -> None

let Mutation =
    Define.Object<Root>(
        name = "Mutation",
        fields =
            [ Define.Field("createChannel", ChannelType, "Creates a new channel on the server.",
                [ Define.Input("name", String); Define.Input("description", String); Define.Input("users", ListOf String) ], createChannel)
              Define.Field("addUserToChannel", Nullable ChannelType, "Adds an user to a channel on the server.",
                [ Define.Input("channel", String); Define.Input("user", String) ], addUserToChannel)
              Define.Field("removeUserFromChannel", Nullable ChannelType, "Removes an user of a channel on the server.",
                [ Define.Input("channel", String); Define.Input("user", String) ], removeUserFromChannel)])