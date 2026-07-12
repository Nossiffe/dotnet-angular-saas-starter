using ErrorOr;
using Saas.Application.Auth;
using Saas.Domain.Products;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Common;

public sealed class DomainErrorsTests
{
    public static TheoryData<Error, ErrorType, string> Registre => new()
    {
        { ProductErrors.NotFound, ErrorType.NotFound, "Product.NotFound" },
        { OrganizationErrors.NotFound, ErrorType.NotFound, "Organization.NotFound" },
        { OrganizationErrors.MemberNotFound, ErrorType.NotFound, "Membership.NotFound" },
        { OrganizationErrors.LastOwner, ErrorType.Validation, "Organization.LastOwner" },
        { OrganizationErrors.AlreadyMember, ErrorType.Validation, "Membership.AlreadyMember" },
        { OrganizationErrors.AlreadyJoined, ErrorType.Conflict, "Membership.AlreadyJoined" },
        { InvitationErrors.NotFound, ErrorType.NotFound, "Invitation.NotFound" },
        { InvitationErrors.OwnerRole, ErrorType.Validation, "Invitation.OwnerRole" },
        { InvitationErrors.AlreadyPending, ErrorType.Conflict, "Invitation.AlreadyPending" },
        { InvitationErrors.NotPendingRevoke, ErrorType.Validation, "Invitation.NotPending" },
        { InvitationErrors.NotPendingAccept, ErrorType.Validation, "Invitation.NotPending" },
        { InvitationErrors.NotPendingRefresh, ErrorType.Validation, "Invitation.NotPending" },
        { InvitationErrors.Expired, ErrorType.Validation, "Invitation.Expired" },
        { InvitationErrors.Invalid, ErrorType.Validation, "Invitation.Invalid" },
        { AuthErrors.InvalidCredentials, ErrorType.Unauthorized, "Auth.InvalidCredentials" },
        { AuthErrors.SessionExpired, ErrorType.Unauthorized, "Auth.SessionExpired" },
        { AuthErrors.InvalidConfirmationLink, ErrorType.Validation, "Auth.InvalidConfirmationLink" },
    };

    [Theory]
    [MemberData(nameof(Registre))]
    public void Chaque_erreur_a_le_bon_type_et_code(Error error, ErrorType type, string code)
    {
        error.Type.ShouldBe(type);
        error.Code.ShouldBe(code);
        error.Description.ShouldNotBeNullOrWhiteSpace();
        error.Description.ShouldEndWith(".");
    }
}
