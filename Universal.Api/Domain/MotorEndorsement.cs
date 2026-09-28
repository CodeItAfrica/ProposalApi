using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Models
{
    // Motor endorsements use the same six types as fire — EndorsementTypes in
    // FireEndorsementModels.cs is shared, not duplicated here. The unit differs: in motor a
    // "section" is one VEHICLE, and a record is still one PolicyDetails row (one calculation).

    /// <summary>
    /// One request for every motor endorsement type. What matters per type:
    ///   ADDITIONAL          Vehicles (+ optional EffectiveDate and rate overrides)
    ///   RETURN / REDO       SelectedVehicleTokens (the ticked vehicles)
    ///   EXTENSION           NewEndDate
    ///   REVERSAL/REDUCTION  nothing beyond the record being endorsed
    /// </summary>
    public class MotorEndorsementRequestDto
    {
        /// <summary>Set from the query string by the controller (policy numbers contain slashes).</summary>
        public string PolicyNo { get; set; }

        /// <summary>ADDITIONAL, REVERSAL (DELETE/DELETED), RETURN, REDO (screen: NEW), EXTENSION, REDUCTION.</summary>
        [Required]
        public string EndorsementType { get; set; }

        /// <summary>DetailID of the record (PolicyDetails row) being endorsed.</summary>
        [Required]
        public string SourceDetailID { get; set; }

        /// <summary>RETURN / REDO: SelectionToken values from GET endorsement/motor/vehicles.</summary>
        public List<string> SelectedVehicleTokens { get; set; } = new();

        /// <summary>ADDITIONAL: the new vehicles, priced like any motor calculation.</summary>
        public List<MotorRiskItemInputDto> Vehicles { get; set; } = new();

        /// <summary>ADDITIONAL: date the new vehicles go on cover (default today). Pro-rated from here to the policy's expiry.</summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>EXTENSION: the new end date — must be after the record's current end date.</summary>
        public DateTime? NewEndDate { get; set; }

        // ADDITIONAL only — leave null to use the endorsed record's values.
        public decimal? ProportionRate { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string Currency { get; set; }
        public decimal? OtherDiscountRate { get; set; }
        public decimal? OtherLoadingRate { get; set; }

        /// <summary>
        /// PolicyDetails.Field4 for ADDITIONAL / REDO / EXTENSION / REDUCTION.
        /// REVERSAL and RETURN always use their fixed descriptions.
        /// </summary>
        public string Description { get; set; }

        public string Remarks { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
    }

    public class MotorEndorsementResponseDto
    {
        public string PolicyNo { get; set; }

        /// <summary>Canonical type — the new record's BizOption.</summary>
        public string EndorsementType { get; set; }

        /// <summary>The new record's EndorsementNo — also the value stamped on original vehicles.</summary>
        public string EndorsementNo { get; set; }

        public string CertOrDocNo { get; set; }
        public string NewDetailID { get; set; }

        /// <summary>PolicyDetails.Field4.</summary>
        public string Description { get; set; }

        /// <summary>PolicyDetails.Field50 — UNCOMPLETED until the endorsement is completed.</summary>
        public string Status { get; set; }

        public string SourceDetailID { get; set; }
        public string SourceCertOrDocNo { get; set; }

        /// <summary>True for REVERSAL and REDO, which set the endorsed record to EXPIRED.</summary>
        public bool SourceRecordExpired { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int CoverDays { get; set; }

        public int VehicleCount { get; set; }

        /// <summary>Original vehicles set to EXPIRED by this endorsement.</summary>
        public List<string> ExpiredVehicleTokens { get; set; } = new();

        /// <summary>Original vehicles stamped with this endorsement's number.</summary>
        public List<string> StampedVehicleTokens { get; set; } = new();

        /// <summary>Certificate numbers issued to the vehicles this endorsement created.</summary>
        public List<string> NewCertificateNos { get; set; } = new();

        public decimal TotalVehicleValue { get; set; }
        public decimal TotalSumInsured { get; set; }

        /// <summary>Vehicles after vehicle-level adjustments, before the proposal-level ones.</summary>
        public decimal TotalGrossPremium { get; set; }

        public decimal ProRataPremium { get; set; }
        public decimal PremiumDue { get; set; }

        public List<string> Warnings { get; set; } = new();
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    /// <summary>A vehicle on a record, for the RETURN / REDO tick list.</summary>
    public class EndorsableMotorVehicleDto
    {
        /// <summary>Send this back in SelectedVehicleTokens.</summary>
        public string SelectionToken { get; set; }

        /// <summary>PolicyTempDetails.ENDTNum — the vehicle's certificate number.</summary>
        public string CertificateNo { get; set; }

        public string VehicleRegNo { get; set; }
        public string VehicleMake { get; set; }
        public string VehicleModel { get; set; }
        public string VehicleType { get; set; }
        public string CoverType { get; set; }
        public string Usage { get; set; }
        public string VehicleUser { get; set; }
        public string Tag { get; set; }

        /// <summary>Already off cover from an earlier endorsement — can't be ticked.</summary>
        public bool IsExpired { get; set; }

        /// <summary>Endorsement number stamped on the vehicle, if any.</summary>
        public string StampedBy { get; set; }

        public decimal VehicleValue { get; set; }
        public decimal SumInsured { get; set; }
        public decimal NetPremium { get; set; }
        public int AdjustmentCount { get; set; }
    }

    /// <summary>One endorsement record on a motor policy.</summary>
    public class MotorEndorsementListItemDto
    {
        public string DetailID { get; set; }
        public string EndorsementNo { get; set; }
        public string CertOrDocNo { get; set; }

        /// <summary>BizOption — the type, or EXPIRED once a later endorsement expired it.</summary>
        public string EndorsementType { get; set; }

        /// <summary>Field4.</summary>
        public string Description { get; set; }

        /// <summary>Field50 — UNCOMPLETED / COMPLETED.</summary>
        public string Status { get; set; }

        public DateTime? EntryDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalVehicleValue { get; set; }
        public decimal PremiumDue { get; set; }
        public string CreatedBy { get; set; }
        public string Remarks { get; set; }
    }
}
