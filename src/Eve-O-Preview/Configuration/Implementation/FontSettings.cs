//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

namespace EveOPreview.Configuration.Implementation
{
    using System;
    using System.Drawing;

    public class FontSettings
    {
        public string Name { get; set; } = "Arial";
        public FontStyle Style { get; set; }
        public float Size { get; set; } = 14.25f;
        public Color ForeColor { get; set; } = Color.FromArgb(255, 255, 165, 0);
        public Color OutlineColor { get; set; } = Color.Black;
        public float OutlineWidth { get; set; } = 3;
        public int PositionOffsetFromLeft { get; set; } = 10;
        public int PositionOffsetFromTop { get; set; } = 5;
    }
}