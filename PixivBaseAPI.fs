﻿namespace PixivFS

open FSharp.Data
open FSharp.Data.JsonExtensions
open System

exception public PixivException of string

type PixivBaseAPI() =
    member val private client_id = "MOBrBDS8blbauoSck0ZfDbtuzpyT" with get, set
    member val private client_secret = "lsACyCD94FhDUtGTXi3QzcFE2uU1hqtDaKeqrdwj" with get, set
    member val private access_token : string = null with get, set
    member val private refresh_token : string = null with get, set
    member val private user_id = 0 with get, set

    member __.require_auth =
        if __.access_token = null then
            "Authentication required! Call login() or set_auth() first!"
            |> PixivException
            |> raise

    member __.set_auth (access_token, ?refresh_token) =
        let refresh_token = defaultArg refresh_token null
        __.access_token <- access_token
        __.refresh_token <- refresh_token

    //用户名密码登录
    member __.login (username, password) = __.auth (username, password)

    member __.set_client (client_id, client_secret) =
        __.client_id <- client_id
        __.client_secret <- client_secret

    //auth主要逻辑
    //refresh_token未测试
    member __.auth (?username, ?password, ?refresh_token) =
        let username = defaultArg username null
        let password = defaultArg password null
        let refresh_token = defaultArg refresh_token null
        let url = "https://oauth.secure.pixiv.net/auth/token"
        let headers = [ "User-Agent", "PixivAndroidApp/5.0.64 (Android 6.0)" ]

        let mutable data =
            [ "get_secure_url", "1"
              "client_id", __.client_id
              "client_secret", __.client_secret ]
        if (not (isNull username)) && (not (isNull password)) then
            data <- data @ [ "grant_type", "password"
                             "username", username
                             "password", password ]
        else if (not (isNull refresh_token)) || (not (isNull __.refresh_token)) then
            data <- data @ [ "grant_type", "refresh_token"
                             "refresh_token",
                             (if isNull refresh_token then __.refresh_token
                              else refresh_token) ]
        else
            "[ERROR] auth() but no password or refresh_token is set."
            |> PixivException
            |> raise
        let r =
            Http.Request
                (url, headers = headers, httpMethod = "POST",
                 body = FormValues data)
        if not (List.contains r.StatusCode [ 200; 301; 302 ]) then
            if List.contains ("grant_type", "password") data then
                "[ERROR] auth() failed! check username and password."
                |> PixivException
                |> raise
            else
                "[ERROR] auth() failed! check refresh_token."
                |> PixivException
                |> raise
        let mutable token = JsonValue.Null
        try
            let mutable resjson = r.Body.ToString()
            resjson <- resjson.Substring(0, resjson.LastIndexOf("\""))
            resjson <- resjson.Substring(resjson.IndexOf("\"") + 1)
            token <- resjson |> JsonValue.Parse
            __.access_token <- token?response?access_token.AsString()
            __.user_id <- token?response?user?id.AsInteger()
            __.refresh_token <- token?response?refresh_token.AsString()
        with e ->
            ("Get access_token error! Exception:\n{0}\nResponse:\n{1}",
             e.Message, r.Body.ToString())
            |> String.Format
            |> PixivException
            |> raise
        token