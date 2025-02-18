namespace OfficeInterop

open Shared
open TermTypes
open OfficeInteropTypes

type FillHiddenColsState =
| Inactive
| ExcelCheckHiddenCols
| ServerSearchDatabase
| ExcelWriteFoundTerms
    member this.toReadableString =
        match this with
        | Inactive          -> ""
        | ExcelCheckHiddenCols  -> "Check Hidden Cols"
        | ServerSearchDatabase  -> "Search Database"
        | ExcelWriteFoundTerms  -> "Write Terms"

type Model = {
    HasAnnotationTable          : bool
    FillHiddenColsStateStore    : FillHiddenColsState
} with
    static member init () = {
        HasAnnotationTable  = false
        FillHiddenColsStateStore = Inactive
    }

type Msg =
    // create and update table element functions
    | CreateAnnotationTable                 of tryUsePrevOutput:bool 
    | AnnotationtableCreated
    | AnnotationTableExists                 of TryFindAnnoTableResult
    | InsertOntologyTerm                    of TermMinimal
    | AddAnnotationBlock                    of InsertBuildingBlock
    | AddAnnotationBlocks                   of InsertBuildingBlock [] //* OfficeInterop.Types.Xml.ValidationTypes.TableValidation option
    | ImportFile                            of (string*InsertBuildingBlock []) []
    | RemoveBuildingBlock
    | UpdateUnitForCells                    of unitTerm:TermMinimal
    | AutoFitTable                          of hideRefCols:bool
    // Term search functions
    | GetParentTerm
    // custom xml functions
    | GetTableValidationXml
    | WriteTableValidationToXml             of newTableValidation:CustomXmlTypes.Validation.TableValidation * currentSwateVersion:string
    | DeleteAllCustomXml
    | GetSwateCustomXml
    | UpdateSwateCustomXml                  of string
    // table+database interconnected functions
    /// 
    | FillHiddenColsRequest
    ///
    | FillHiddenColumns                     of TermSearchable []
    ///
    | UpdateFillHiddenColsState             of FillHiddenColsState
    /// Show Details to selected BuildingBlock
    | GetSelectedBuildingBlockTerms
    //
    ///
    | InsertFileNames                       of fileNameList:string list
    // Development
    | TryExcel
    | TryExcel2
    //| ExcelSubscriptionMsg          of OfficeInterop.Types.Subscription.Msg

