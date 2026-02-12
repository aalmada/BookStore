namespace BookStore.ApiService.Messages.Commands;

public record SendUserVerificationEmail(
	Guid UserId,
	string Email,
	string VerificationCode,
	string UserName,
	string TenantId);
