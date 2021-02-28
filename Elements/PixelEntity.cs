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
        public char character;
        public Vector2f position;
        public Color color;
        public static Font font = Converter.font;
        public static Text text = new Text(" ", font);

        public PixelEntity(char _character, Color _color, Vector2f _position)
        {
            character = (char)_character;
            position = _position;
            color = _color;
        }

        public Text GetPixel(uint scale)
        {
            text.FillColor = color;
            text.DisplayedString = character.ToString();
            text.Position = new Vector2f(position.X * scale, position.Y * scale);

            return text;
        }
    }
}