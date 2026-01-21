namespace BookStore.Client;

public interface IPasskeyClient :
    IGetPasskeyCreationOptionsEndpoint,
    IRegisterPasskeyEndpoint,
    IGetPasskeyLoginOptionsEndpoint,
    ILoginPasskeyEndpoint,
    IListPasskeysEndpoint,
    IDeletePasskeyEndpoint
{
}
