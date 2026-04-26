namespace GoatLab.Server.Services.Legal;

// Lawyer-fillable values that the Terms / Privacy pages substitute into their
// bracketed placeholders. All optional — when blank the original [BRACKETED]
// text shows through, signalling "not yet set" to readers and to the lawyer.
//
// Set via env vars:
//   LEGAL__ENTITYNAME, LEGAL__ENTITYTYPE, LEGAL__STATE, LEGAL__BUSINESSADDRESS,
//   LEGAL__CONTACTEMAIL, LEGAL__GOVERNINGLAWSTATE, LEGAL__GOVERNINGLAWCOUNTY,
//   LEGAL__GOVERNINGLAWCITY, LEGAL__DISPUTERESOLUTION (court|arbitration),
//   LEGAL__APPROVED (true once your attorney signs off — flips off the warning banner).
public class LegalOptions
{
    public const string SectionName = "Legal";

    public string? EntityName { get; set; }              // e.g. "Acme Goat Software, LLC"
    public string? EntityType { get; set; }              // e.g. "LLC" / "corporation" / "sole proprietor"
    public string? State { get; set; }                   // e.g. "Nevada" — formation state for the entity
    public string? BusinessAddress { get; set; }         // e.g. "123 Main St, Reno, NV 89501"
    public string? ContactEmail { get; set; }            // e.g. "legal@goatlab.app"

    public string? GoverningLawState { get; set; }       // e.g. "Nevada"
    public string? GoverningLawCounty { get; set; }      // e.g. "Washoe County, Nevada"
    public string? GoverningLawCity { get; set; }        // e.g. "Reno, Nevada"

    /// <summary>"court" or "arbitration". Selects which disputes paragraph renders.</summary>
    public string? DisputeResolution { get; set; }

    /// <summary>
    /// Set true once a qualified attorney has reviewed the deployed final text.
    /// Hides the prominent "Draft, not legal advice" warning banner. Leave false
    /// until that has actually happened — flipping this without lawyer review
    /// undermines the very protection these documents exist to provide.
    /// </summary>
    public bool Approved { get; set; } = false;
}
