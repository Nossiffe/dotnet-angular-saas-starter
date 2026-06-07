namespace Saas.Application.Common.Interfaces;

/// <summary>Enfile une demande de reset de mot de passe vers un job d'arrière-plan (ferme le timing oracle).</summary>
public interface IPasswordResetEnqueuer
{
    void Enqueue(string email);
}
