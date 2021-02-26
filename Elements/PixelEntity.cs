using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public Text text;
        public Font font;

        public PixelEntity(byte _character, Color _color, Vector2f _position, Font _font)
        {
            character = _character;
            position = _position;
            color = _color;
            font = _font;

            text = new Text(((char)character).ToString(), font);
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