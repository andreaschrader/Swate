module CustomComponents.Navbar

open Fable.React
open Fable.React.Props
open Fulma

open ExcelColors
open Model
open Messages

open Fable.FontAwesome

let navbarComponent (model : Model) (dispatch : Msg -> unit) =
    Navbar.navbar [Navbar.Props [Props.Role "navigation"; AriaLabel "main navigation" ; ExcelColors.colorElement model.SiteStyleState.ColorMode]] [
        Navbar.Brand.a [] [
            Navbar.Item.a [Navbar.Item.Props [Props.Href "https://csb.bio.uni-kl.de/"]] [
                img [Props.Src "../assets/CSB_Logo.png"]
            ]
            Navbar.Item.a [Navbar.Item.Props [Title "Add New Annotation Table"; Style [ Color model.SiteStyleState.ColorMode.Text]]] [
                Button.a [
                    Button.Props [Style [BackgroundColor model.SiteStyleState.ColorMode.ElementBackground]]
                    Button.OnClick (fun _ ->
                        (fun (allNames) ->
                            CreateAnnotationTable (allNames,model.SiteStyleState.IsDarkMode))
                            |> PipeCreateAnnotationTableInfo
                            |> ExcelInterop
                            |> dispatch
                    )
                    Button.Color Color.IsWhite
                    Button.IsInverted
                ] [
                    Fa.span [Fa.Solid.Plus][]
                    Fa.span [Fa.Solid.Table][]
                ]
            ]
            Navbar.Item.a [Navbar.Item.Props [Title "Autoformat Table"; Style [ Color model.SiteStyleState.ColorMode.Text]]] [
                Button.a [
                    Button.Props [Style [BackgroundColor model.SiteStyleState.ColorMode.ElementBackground]]
                    Button.OnClick (fun e -> PipeActiveAnnotationTable AutoFitTable |> ExcelInterop |> dispatch )
                    Button.Color Color.IsWhite
                    Button.IsInverted
                ] [
                    Fa.i [Fa.Solid.SyncAlt][]
                ]
            ]
            Navbar.Item.a [Navbar.Item.Props [Title "Update Reference Columns"; Style [ Color model.SiteStyleState.ColorMode.Text]]] [
                Button.a [
                    Button.Props [Style [BackgroundColor model.SiteStyleState.ColorMode.ElementBackground]]
                    Button.OnClick (fun _ ->
                        PipeActiveAnnotationTable FillHiddenColsRequest |> ExcelInterop |> dispatch
                    )
                    Button.Color Color.IsWhite
                    Button.IsInverted
                ] [
                    Fa.span [Fa.Solid.EyeSlash][]
                    span [][str model.ExcelState.FillHiddenColsStateStore.toReadableString]
                    Fa.span [Fa.Solid.Pen][]
                ]
            ]
            Navbar.burger [ Navbar.Burger.IsActive model.SiteStyleState.BurgerVisible
                            Navbar.Burger.OnClick (fun e -> ToggleBurger |> StyleChange |> dispatch)
                            Navbar.Burger.Props[
                                    Role "button"
                                    AriaLabel "menu"
                                    Props.AriaExpanded false
                            ]
            ] [
                span [AriaHidden true] []
                span [AriaHidden true] []
                span [AriaHidden true] []
            ]
        ]
        Navbar.menu [Navbar.Menu.Props [Id "navbarMenu"; Class (if model.SiteStyleState.BurgerVisible then "navbar-menu is-active" else "navbar-menu") ; ExcelColors.colorControl model.SiteStyleState.ColorMode]] [
            Navbar.Dropdown.div [ ] [
                Navbar.Item.a [Navbar.Item.Props [Style [ Color model.SiteStyleState.ColorMode.Text]]] [
                    str "How to use"
                ]
                Navbar.Item.a [Navbar.Item.Props [Style [ Color model.SiteStyleState.ColorMode.Text]]] [
                    str "Contact"
                ]
                Navbar.Item.a [Navbar.Item.Props [
                    OnClick (fun e ->
                        ToggleBurger |> StyleChange |> dispatch
                        UpdatePageState (Some Routing.Route.Settings) |> dispatch
                    )
                    Style [ Color model.SiteStyleState.ColorMode.Text]
                ]] [
                    str "Settings"
                ]
                Navbar.Item.a [Navbar.Item.Props [
                    Style [ Color model.SiteStyleState.ColorMode.Text];
                    OnClick (fun e ->
                        ToggleBurger |> StyleChange |> dispatch
                        UpdatePageState (Some Routing.Route.ActivityLog) |> dispatch
                    )
                ]] [
                    str "Activity Log"
                ]
            ]
        ]
    ]