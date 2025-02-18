module Spreadsheet.TypeConverter

open Shared
open OfficeInteropTypes
open Spreadsheet

type InsertBuildingBlock with
    member this.toSwateBuildingBlock(index:int) : SwateBuildingBlock =
        let header =
            let term = this.ColumnTerm |> Option.defaultValue (TermTypes.TermMinimal.create this.ColumnHeader.Name "")
            HeaderCell.create(this.ColumnHeader.Type, term = term, hasUnit = this.HasUnit)
        let rows =
            match this.HasValues, header.isTermColumn, this.HasUnit with
            | _, true, true       -> // even if no values exist, we want to add unit to body cells.
                if this.Rows.Length = 0 then
                    Array.init 1 (fun i -> i + 1, SwateCell.create("", ?unit = this.UnitTerm))
                else
                    this.Rows |> Array.mapi (fun i t -> i + 1, SwateCell.create(t.Name, ?unit = this.UnitTerm))
            | true, true, false   -> this.Rows |> Array.mapi (fun i t -> i + 1, SwateCell.create(t))
            | true, false, _      -> this.Rows |> Array.mapi (fun i t -> i + 1, SwateCell.create(t.Name))
            | false, _, _         -> [||]
        SwateBuildingBlock.create(index, header, rows)

type SwateBuildingBlock with
    member this.toBuildingBlock : BuildingBlock =
        let mutable nextIndex = this.Index
        let rows = this.Rows
        let mainColumn =
            let h = SwateColumnHeader.create this.Header.DisplayValue
            let r = rows |> Array.map (fun (ind,x) ->
                match x with
                | IsUnit c -> Cell.create ind (Some c.Value) (Some c.Unit)
                | IsTerm c -> Cell.create ind (Some c.Term.Name) None
                | IsFreetext c -> Cell.create ind (Some c.Value) None
                | IsHeader _ -> failwith "Body cell conversion should not happen on header."
            )
            Column.create nextIndex h r
        let unit =
            if this.Header.HasUnit then
                let h = SwateColumnHeader.create(ColumnCoreNames.Unit.toString)
                let r = rows |> Array.map (fun (ind,x) ->
                    match x with
                    | IsUnit c -> Cell.create ind (Some c.Unit.Name) None
                    | IsTerm _ | IsFreetext _ | IsHeader _ -> failwith "TSR conversion should not happen on freetext column or header."
                )
                nextIndex <- nextIndex + 1 
                Column.create nextIndex h r |> Some
            else
                None
        let tsr =
            if this.Header.isTermColumn then
                let h = SwateColumnHeader.create($"{ColumnCoreNames.TermSourceRef.toString} ({this.Header.Term.Value.TermAccession})")
                let r = rows |> Array.map (fun (ind,x) ->
                    match x with
                    | IsUnit c -> Cell.create ind (Some c.Unit.accessionToTSR) None
                    | IsTerm c -> Cell.create ind (Some c.Term.accessionToTSR) None
                    | IsFreetext _ | IsHeader _ -> failwith "TSR conversion should not happen on freetext column or header."
                )
                nextIndex <- nextIndex + 1 
                Column.create nextIndex h r |> Some
            else
                None
        let tan =
            if this.Header.isTermColumn then
                let h = SwateColumnHeader.create($"{ColumnCoreNames.TermAccessionNumber.toString} ({this.Header.Term.Value.TermAccession})")
                let r = rows |> Array.map (fun (ind,x) ->
                    match x with
                    | IsUnit c -> Cell.create ind (Some c.Unit.accessionToTAN) None
                    | IsTerm c -> Cell.create ind (Some c.Term.accessionToTAN) None
                    | IsFreetext _ | IsHeader _ -> failwith "TSR conversion should not happen on freetext column or header."
                )
                nextIndex <- nextIndex + 1 
                Column.create nextIndex h r |> Some
            else
                None
        BuildingBlock.create mainColumn tsr tan unit this.Header.Term

type TermTypes.TermSearchable with
    ///<summary>Converts TermSearchable to SwateCell type. If no search result is found returns previous cell `sc`.</summary>
    member this.toSwateCell (sc:SwateCell) =
        let isHeader = this.RowIndices = [|0|]
        let isUnit = this.IsUnit
        if this.SearchResultTerm.IsNone then
            sc
        else
            match this.SearchResultTerm, sc with
            | Some res, IsHeader c when isHeader -> IsHeader {c with Term = TermTypes.TermMinimal.ofTerm res |> Some}
            | Some res, IsUnit c when isUnit -> IsUnit {c with Unit = TermTypes.TermMinimal.ofTerm res}
            | Some res, IsTerm c -> IsTerm {c with Term = TermTypes.TermMinimal.ofTerm res}
            | anythingElse -> failwithf "Unable to convert search results to table: [Current_Cell] %A; [SearchResults] %A" anythingElse this

module SwateBuildingBlock =
    
    ///<summary> Parse column of index `index` from ActiveTableMap `m` to SwateBuildingBlock. </summary>
    let ofTableMap_byIndex (index: int) (m: Map<int*int,SwateCell>) =
        let column = Map.filter (fun k _ -> fst k = index ) m
        let header = column.[index, 0].Header
        let rows = [|
            for KeyValue ((_,rk),c) in column do
                if rk <> 0 then
                    yield rk, c
        |]
        SwateBuildingBlock.create(index, header, rows)

    ///<summary>Returns a list instead of array.</summary>
    let ofTableMap_list (m: Map<int*int,SwateCell>) : SwateBuildingBlock list =
        let maxColIndex = m.Keys |> Seq.maxBy fst |> fst
        [
            for i in 0 .. maxColIndex do
                yield ofTableMap_byIndex i m
        ]

    let ofTableMap (m: Map<int*int,SwateCell>) : SwateBuildingBlock [] =
        let maxColIndex = m.Keys |> Seq.maxBy fst |> fst
        [|
            for i in 0 .. maxColIndex do
                yield ofTableMap_byIndex i m
        |]

    let toTableMap (buildingBlocks: seq<SwateBuildingBlock>) : Map<int*int,SwateCell> =
        buildingBlocks
        |> Seq.collect (fun bb ->
            let columnIndex = bb.Index
            let header = (columnIndex, 0), IsHeader bb.Header
            let rows =
                bb.Rows |> Array.map (fun (i,c) ->
                    (columnIndex, i), c
                )
            [|
                yield header
                yield! rows
            |]
        )
        |> Map.ofSeq
        

