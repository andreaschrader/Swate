module MainComponents.NoTablesElement

open Feliz
open Feliz.Bulma

open Spreadsheet
open Messages
open Browser.Types
open Fable.Core.JsInterop

open Elmish

let private buttonStyle = prop.style [style.flexDirection.column; style.height.unset; style.width(length.px 140); style.margin(length.rem 1.5)]

module private UploadHandler =

    open Fable.Core.JsInterop

    let mutable styleCounter = 0

    [<Literal>]
    let id = "droparea"
    let updateMsg = fun r -> r |> ParseFileUpload |> SpreadsheetMsg

    let setActive_DropArea() =
        styleCounter <- styleCounter + 1
        let ele = Browser.Dom.document.getElementById(id)
        ele?style?border <- $"2px solid {NFDIColors.Mint.Base}"

    let setInActive_DropArea() =
        styleCounter <- (System.Math.Max(styleCounter - 1,0))
        if styleCounter <= 0 then
            let ele = Browser.Dom.document.getElementById(id)
            ele?style?border <- "unset"

    let ondrop dispatch =
        fun (e: Browser.Types.DragEvent) ->
            e.preventDefault()
            if e.dataTransfer.items <> null then
                let item = e.dataTransfer.items.[0]
                if item.kind = "file" then
                    setInActive_DropArea()
                    styleCounter <- 0
                    let file = item.getAsFile()
                    let reader = Browser.Dom.FileReader.Create()
                    reader.onload <- (fun _ -> updateMsg !!reader.result |> dispatch)
                    reader.readAsArrayBuffer(file)

let private uploadNewTable dispatch =
    let uploadId = "UploadFiles_MainWindowInit"
    Bulma.label [
        //prop.onDragEnter <| UploadHandler.dontBubble
        //prop.onDragLeave <| UploadHandler.dontBubble
        prop.style [style.fontWeight.normal]
        prop.children [
            Html.input [
                prop.id uploadId
                prop.type' "file";
                prop.style [style.display.none]
                prop.onChange (fun (ev: Event) ->
                    let fileList : FileList = ev.target?files

                    if fileList.length > 0 then
                        let file = fileList.item 0 |> fun f -> f.slice()

                        let reader = Browser.Dom.FileReader.Create()

                        reader.onload <- fun evt ->
                            let (r: byte []) = evt.target?result
                            r |> ParseFileUpload |> SpreadsheetMsg |> dispatch
                                   
                        reader.onerror <- fun evt ->
                            curry GenericLog Cmd.none ("Error", evt?Value) |> DevMsg |> dispatch

                        reader.readAsArrayBuffer(file)
                    else
                        ()
                    let picker = Browser.Dom.document.getElementById(uploadId)
                    // https://stackoverflow.com/questions/3528359/html-input-type-file-file-selection-event/3528376
                    picker?value <- null
                    ()
                )
            ]
            Bulma.button.span [
                Bulma.button.isLarge
                buttonStyle
                Bulma.color.isInfo
                prop.onClick(fun e ->
                    e.preventDefault()
                    let getUploadElement = Browser.Dom.document.getElementById uploadId
                    getUploadElement.click()
                    ()
                )
                prop.children [
                    Html.div [
                        Html.i [prop.className "fas fa-plus"]
                        Html.i [prop.className "fas fa-table"]
                    ]
                    Html.div "Import File"
                ]
            ]
        ]
    ]

let createNewTable dispatch =
    Bulma.button.span [
        //prop.onDragEnter <| UploadHandler.dontBubble
        //prop.onDragLeave <| UploadHandler.dontBubble
        Bulma.button.isLarge
        buttonStyle
        Bulma.color.isPrimary
        prop.onClick(fun e -> SpreadsheetInterface.CreateAnnotationTable e.ctrlKey |> Messages.InterfaceMsg |> dispatch)
        prop.children [
            Html.div [
                Html.i [prop.className "fas fa-plus"]
                Html.i [prop.className "fas fa-table"]
            ]
            Html.div "New Table"
        ]
    ]

let Main (dispatch: Messages.Msg -> unit) =
    Html.div [
        prop.id UploadHandler.id
        prop.onDragEnter (fun e ->
            e.preventDefault()
            if e.dataTransfer.items <> null then
                let item = e.dataTransfer.items.[0]
                if item.kind = "file" then
                    UploadHandler.setActive_DropArea()
        )
        prop.onDragLeave(fun e ->
            //e.preventDefault()
            UploadHandler.setInActive_DropArea()
        )
        prop.onDragOver(fun e -> e.preventDefault())
        prop.onDrop <| UploadHandler.ondrop dispatch
        prop.style [
            style.height.inheritFromParent
            style.width.inheritFromParent
            style.display.flex
            style.justifyContent.center
            style.alignItems.center
        ]
        prop.children [
            Html.div [
                prop.style [style.height.minContent; style.display.inheritFromParent; style.justifyContent.spaceBetween]
                prop.children [
                    createNewTable dispatch
                    uploadNewTable dispatch
                ]
            ]
        ]
    ]