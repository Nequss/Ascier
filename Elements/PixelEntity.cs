using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ascier.Converters.Base;
using ImageMagick;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Ascier.Elements
{
    public class PixelEntity
    {
        public byte character;
        public Vector2f position;
        public Color color;
        public static Font font = Converter.font;
        public static Text text = new Text(" ", font);

        public PixelEntity(byte _character, Color _color, Vector2f _position)
        {
            character = _character;
            position = _position;
            color = _color;

            text.DisplayedString = character.ToString();
        }

        public Text GetPixel(uint scale)
        {
            text.FillColor = color;
            text.CharacterSize = scale;
            text.Position = new Vector2f(position.X * scale, position.Y * scale);

            return text;
        }
    }
}