namespace AscierWeb.Core.Effects;

// interfejs efektu ascii - każdy efekt przetwarza surowe piksele rgb24
// na ramkę ascii z opcjonalnymi kolorami
public interface IEffect
{
    // unikalna nazwa efektu
    string Name { get; }

    // opis efektu (po polsku)
    string Description { get; }

    // przetwarza surowe dane rgb24 na ramkę ascii
    // rgb24: tablica bajtów [r,g,b,r,g,b,...] - 3 bajty na piksel
    // width/height: wymiary źródłowego obrazu w pikselach
    AsciiFrame Process(byte[] rgb24, int width, int height, ConversionSettings settings);
}
