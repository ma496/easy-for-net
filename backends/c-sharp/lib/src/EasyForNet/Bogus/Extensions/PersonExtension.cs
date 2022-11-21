using Bogus;

namespace EasyForNet.Bogus.Extensions;

public static class PersonExtension
{
    public static string IdCard(this Person person)
    {
        string idCard = null;
        for (var i = 0; i < 4; i++)
        {
            var idCardPart = person.Random.String(4, '\u0030', '\u0039');
            if (string.IsNullOrWhiteSpace(idCard))
                idCard = idCardPart;
            else
                idCard += $"-{idCardPart}";
        }

        return idCard;
    }
}