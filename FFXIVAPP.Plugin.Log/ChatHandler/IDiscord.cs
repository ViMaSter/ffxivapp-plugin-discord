// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Plugin.cs" company="SyndicatedLife">
//   Copyright(c) 2018 Ryan Wilson &amp;lt;syndicated.life@gmail.com&amp;gt; (http://syndicated.life/)
//   Licensed under the MIT license. See LICENSE.md in the solution root for full license information.
// </copyright>
// <summary>
//   Plugin.cs Implementation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace FFXIVAPP.Plugin.Log.ChatHandler
{
    public interface IDiscord
    {
        void Broadcast(string message);
        void SetIsActive(bool active);
    }
}