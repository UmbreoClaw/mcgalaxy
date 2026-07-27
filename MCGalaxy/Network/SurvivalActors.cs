/*
    Copyright 2015-2024 MCGalaxy

    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at

    https://opensource.org/license/ecl-2-0/
    https://www.gnu.org/licenses/gpl-3.0.html

    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using MCGalaxy.DB;

namespace MCGalaxy.Network
{
    /// <summary> Virtual block-change authors for the survival simulation.
    /// Their edits are recorded in BlockDB under stable non-player ids (the
    /// same InvalidNameID mechanism "(console)" uses), so /About shows WHAT
    /// destroyed a block - "(creeper)", "(tnt)", "(fire)" - and /UndoPlayer
    /// rolls a cause back by that name (e.g. "/UndoPlayer (creeper) 1h").
    /// Growth/decay/fluids stay attributed to console on purpose: undoing
    /// them by name would desync the crop/farmland sidecar state. </summary>
    public static class SurvivalActors
    {
        public static readonly Player Creeper = new SurvivalActor("(creeper)");
        public static readonly Player Tnt     = new SurvivalActor("(tnt)");
        public static readonly Player Fire    = new SurvivalActor("(fire)");

        sealed class SurvivalActor : Player
        {
            public SurvivalActor(string name) : base(name) {
                group      = Group.ConsoleRank;
                color      = "&S";
                SuperName  = "Survival";
                DatabaseID = NameConverter.InvalidNameID(name);
            }

            public override void Message(string message) { } // no console to talk to
        }
    }
}
