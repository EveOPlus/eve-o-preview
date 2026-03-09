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

using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Helper;

namespace EveOPreview.Mediator.Handlers.Configuration
{
    sealed class RefreshCycleGroupHotkeysHandler : IRequestHandler<RefreshCycleGroupHotkeys>
    {
        private readonly IThumbnailConfiguration _config;

        public RefreshCycleGroupHotkeysHandler(IThumbnailConfiguration Config)
        {
            _config = Config;
        }

        public async Task<Unit> Handle(RefreshCycleGroupHotkeys request, CancellationToken cancellationToken)
        {
            foreach (var cycleGroup in _config.CycleGroups)
            {
                cycleGroup.ForwardHotkeys.RemoveAll(x => x == null);
                cycleGroup.BackwardHotkeys.RemoveAll(x => x == null);

                cycleGroup.ForwardHotkeysParsedAndOrdered.Clear();
                cycleGroup.BackwardHotkeysParsedAndOrdered.Clear();

                var forwardHotkeys = cycleGroup.ForwardHotkeys?.Select(HotkeyHelpers.ToHotkeys);
                if (forwardHotkeys != null)
                {
                    cycleGroup.ForwardHotkeysParsedAndOrdered.AddRange(forwardHotkeys);
                }

                var backwardHotkeys = cycleGroup.BackwardHotkeys?.Select(HotkeyHelpers.ToHotkeys);
                if (backwardHotkeys != null)
                {
                    cycleGroup.BackwardHotkeysParsedAndOrdered.AddRange(backwardHotkeys);
                }
            }

            return Unit.Value;
        }
    }
}