using FluentValidation.TestHelper;
using Saas.Application.Auth.ForgotPassword;
using Saas.Application.Auth.Register;
using Saas.Application.Auth.ResetPassword;
using Saas.Application.Invitations.AcceptInvitation;
using Saas.Application.Invitations.InviteMember;
using Saas.Application.Members.ChangeMemberRole;
using Saas.Application.Products.CreateProduct;
using Saas.Application.Products.UpdateProduct;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Application;

// Les règles qui portent une décision (longueur minimale d'un mot de passe, rôles invitables,
// bornes d'un produit), testées à leur limite. Les « identifiant non vide » ne sont pas répétés ici.
public sealed class ValidatorTests
{
    private const string ValidPassword = "Twelve-chars";

    [Theory]
    [InlineData("Eleven-char", false)]
    [InlineData(ValidPassword, true)]
    public void Register_requires_a_password_of_at_least_12_characters(string password, bool valid)
        => AssertValidity(new RegisterValidator().TestValidate(new RegisterCommand("a@b.ch", password)), valid);

    [Theory]
    [InlineData("Eleven-char", false)]
    [InlineData(ValidPassword, true)]
    public void Reset_requires_a_new_password_of_at_least_12_characters(string password, bool valid)
        => AssertValidity(
            new ResetPasswordValidator().TestValidate(new ResetPasswordCommand(Guid.CreateVersion7(), "token", password)),
            valid);

    // Sans mot de passe : un utilisateur existant accepte l'invitation. Avec : 12 caractères au moins.
    [Theory]
    [InlineData(null, true)]
    [InlineData("Eleven-char", false)]
    [InlineData(ValidPassword, true)]
    public void Accepting_an_invitation_takes_no_password_or_a_long_enough_one(string? password, bool valid)
        => AssertValidity(
            new AcceptInvitationValidator().TestValidate(
                new AcceptInvitationCommand(Guid.CreateVersion7(), "token", password)),
            valid);

    [Theory]
    [InlineData("not-an-email", false)]
    [InlineData("someone@saas.local", true)]
    public void Email_addresses_must_be_well_formed(string email, bool valid)
    {
        AssertValidity(new RegisterValidator().TestValidate(new RegisterCommand(email, ValidPassword)), valid);
        AssertValidity(new ForgotPasswordValidator().TestValidate(new ForgotPasswordCommand(email)), valid);
        AssertValidity(
            new InviteMemberValidator().TestValidate(
                new InviteMemberCommand(Guid.CreateVersion7(), email, OrganizationRole.Member)),
            valid);
    }

    // Owner ne s'invite pas et ne se donne pas : il reste le rôle de celui qui a créé l'organisation.
    [Theory]
    [InlineData(OrganizationRole.Member, true)]
    [InlineData(OrganizationRole.Admin, true)]
    [InlineData(OrganizationRole.Owner, false)]
    [InlineData((OrganizationRole)42, false)]
    public void Only_Member_and_Admin_can_be_invited_or_assigned(OrganizationRole role, bool valid)
    {
        AssertValidity(
            new InviteMemberValidator().TestValidate(new InviteMemberCommand(Guid.CreateVersion7(), "a@b.ch", role)),
            valid);
        AssertValidity(
            new ChangeMemberRoleValidator().TestValidate(
                new ChangeMemberRoleCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), role)),
            valid);
    }

    [Theory]
    [InlineData(200, 0, true)]
    [InlineData(201, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(10, -0.01, false)]
    public void A_product_has_a_name_of_1_to_200_characters_and_a_non_negative_price(
        int nameLength, decimal price, bool valid)
    {
        var name = new string('x', nameLength);
        AssertValidity(new CreateProductValidator().TestValidate(new CreateProductCommand(name, price)), valid);
        AssertValidity(
            new UpdateProductValidator().TestValidate(new UpdateProductCommand(Guid.CreateVersion7(), name, price)),
            valid);
    }

    private static void AssertValidity<T>(TestValidationResult<T> result, bool valid)
        => result.IsValid.ShouldBe(valid, string.Join(" ; ", result.Errors.Select(e => e.ErrorMessage)));
}
