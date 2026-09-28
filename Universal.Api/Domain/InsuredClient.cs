using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models.Customer
{
    /// <summary>
    /// Canonical values for InsuredClients.ClientType. Centralized so nothing
    /// writes an inconsistent literal again - the current code has
    /// CustomerCreate setting "Individual" and CustomerCreateMaster setting
    /// "INDIVIDUAL" (different casing, same table), and neither path ever
    /// writes "Corporate" - IsOrg/OrgName/OrgRegNumber/OrgRegDate are on the
    /// request DTOs but silently dropped before they reach the database.
    /// </summary>
    public static class ClientTypes
    {
        public const string Individual = "Individual";
        public const string Corporate = "Corporate";
    }

    /// <summary>
    /// FIX (this pass): individual vs. corporate customers now share this one
    /// table with NO schema changes - the same discipline already used on the
    /// Gibs.Api/Gibs.Cornerstone.Api onboarding work (CAC/NIN/BVN via
    /// Prembly, runtime-derived corporate discriminator). The reuse mapping,
    /// applied consistently by InsuredRepository's create path:
    ///
    ///   ClientType   -> "Corporate" / "Individual" (see ClientTypes). This
    ///                    IS the discriminator - IsCorporate below just reads it.
    ///   FullName     -> the org's registered/trading name when corporate,
    ///                    "{FirstName} {LastName}" when individual.
    ///   SurName /
    ///   OtherNames   -> always the contact person's name, for BOTH types.
    ///                    For corporate this is the authorized contact, not
    ///                    the business itself - the business name lives in
    ///                    FullName instead.
    ///   NationalID   -> NIN for individuals, CAC registration number for
    ///                    corporates (mirrors the Prembly-verified ID reuse
    ///                    already established elsewhere in GIBS).
    ///   DateofBirth  -> date of birth for individuals, date of incorporation
    ///                    for corporates. Age (below) then reads naturally as
    ///                    "years since incorporation" for a corporate record -
    ///                    no special-casing needed there.
    ///
    /// Gaps that remain honestly unpersisted because no existing column is a
    /// clean fit and this pass deliberately avoids a migration for them
    /// (documented per-field on the DTOs, not silently dropped): KycType,
    /// KycIssueDate, KycExpiryDate. KycNumber is a partial exception - it
    /// continues to ride in Remarks, a pre-existing hack kept as-is rather
    /// than compounded. NextOfKin and CityLGA are NOT in this list anymore -
    /// see the dedicated columns below.
    /// </summary>
    public class InsuredClients
    {
        [Key]
        //[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string InsuredID { get; set; }

        /// <summary>Contact person's surname (both individual and corporate).</summary>
        public string? SurName { get; set; }
        /// <summary>Contact person's other/given names (both individual and corporate).</summary>
        public string? OtherNames { get; set; }
        public string? Title { get; set; }
        public string? Address { get; set; }
        /// <summary>
        /// City/LGA (Local Government Area). NEW COLUMN (this pass) - was
        /// accepted by both create DTOs and silently dropped before (no
        /// column existed for it at all), which is what you were seeing:
        /// it never persisted on create and always came back null on read.
        /// </summary>
        public string? CityLGA { get; set; }
        /// <summary>Individual's full name, OR the corporate customer's registered/trading name.</summary>
        public string? FullName { get; set; }
        public string? State { get; set; }
        public string? Landphone { get; set; }
        public string? MobilePhone { get; set; }
        public string? Email { get; set; }
        /// <summary>NIN for individuals, CAC registration number for corporates.</summary>
        public string? NationalID { get; set; }
        public string? Occupation { get; set; }
        public string? Sex { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Country { get; set; }

        /// <summary>Date of birth for individuals, date of incorporation for corporates.</summary>
        public DateTime? DateofBirth { get; set; }
        /// <summary>Age for individuals, years-since-incorporation for corporates - same computation either way.</summary>
        public decimal? Age { get; set; }

        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }

        public decimal? ReinBalance { get; set; }
        /// <summary>Pre-existing hack (kept, not introduced here): also carries KycNumber.</summary>
        public string? Remarks { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        /// <summary>The individual/corporate discriminator. Always set via ClientTypes - never a raw literal.</summary>
        public string? ClientType { get; set; }

        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? Password { get; set; }

        /// <summary>
        /// Runtime-derived, not persisted ([NotMapped] - no schema impact).
        /// Safe to use after the entity is loaded/materialized. It is NOT
        /// safe to reference inside a LINQ .Where() clause (EF Core can't
        /// reliably translate an arbitrary getter to SQL) - queries that need
        /// to filter by type compare x.ClientType == ClientTypes.Corporate
        /// directly instead (see InsuredRepository.FindDuplicateAsync).
        /// </summary>
        [NotMapped]
        public bool IsCorporate =>
            string.Equals(ClientType, ClientTypes.Corporate, StringComparison.OrdinalIgnoreCase);

        // -----------------------------------------------------------
        // Next-of-kin/alternate-contact fields. NEW COLUMNS (this pass, by
        // request) - a flat set of nine new nullable columns on this same
        // table. Simpler to query (no join/Include needed - see
        // InsuredRepository, which doesn't need .Include for this), at the
        // cost of nine mostly-empty
        // columns on every row and no way to enforce "at most one next of
        // kin" beyond application code (which was already the only
        // enforcement either way, since this is a 1:1 relationship).
        // Requires: ALTER TABLE InsuredClients ADD <these nine columns> -
        // see Migrations/AddNextOfKinColumns.sql. Prefixed NextOfKin* so
        // they can't collide with anything already on this table.
        // -----------------------------------------------------------
        public string? NextOfKinTitle { get; set; }
        public string? NextOfKinFirstName { get; set; }
        public string? NextOfKinLastName { get; set; }
        public string? NextOfKinOtherName { get; set; }
        public string? NextOfKinGender { get; set; }
        public string? NextOfKinEmail { get; set; }
        public string? NextOfKinAddress { get; set; }
        public string? NextOfKinPhoneLine1 { get; set; }
        public string? NextOfKinPhoneLine2 { get; set; }

        /// <summary>
        /// Runtime convenience, not persisted - true when any next-of-kin
        /// column has data. Mirrors IsCorporate's pattern above.
        /// </summary>
        [NotMapped]
        public bool HasNextOfKin =>
            !string.IsNullOrWhiteSpace(NextOfKinFirstName) || !string.IsNullOrWhiteSpace(NextOfKinLastName);
    }

    /// <summary>
    /// DTO backing the PATCH endpoint on CustomersController. Kept in this
    /// namespace (Models.Customer), same as the original file - the
    /// controller imports both this namespace and Contracts.V1, and this is
    /// where it already resolves from.
    ///
    /// Every field is optional - only non-null fields are applied (see
    /// InsuredRepository.ApplyUpdate). Deliberately narrower than the create
    /// requests: identity/KYC fields (name, NationalID, DateOfBirth,
    /// OrgRegNumber/OrgRegDate, etc.) are excluded on purpose so this
    /// endpoint can't be used to silently rewrite who a customer legally is
    /// (individual or corporate) - if that's ever needed, it belongs behind
    /// a separate, audited endpoint. Works unchanged for both individual and
    /// corporate rows, since it only ever touches the shared contact fields.
    /// </summary>
    public class UpdateCustomerRequest
    {
        [EmailAddress]
        public string Email { get; set; }
        public string Address { get; set; }
        public string MobilePhone { get; set; }
        public string Landphone { get; set; }
        public string OtherNames { get; set; }
    }
}