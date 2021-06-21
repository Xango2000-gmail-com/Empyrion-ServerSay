using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using statement for API2 to work. Requires Reference to ModApi.DLL (see solution Explorer on the right)
using Eleon;

//using statement for API1 to work. Requires Refence to Mif.DLL (see solution Explorer on the right)
using Eleon.Modding;

namespace ServerSay
{
    //all my mods have the main class named "MyEmpyrionmod", I do not know if that is mandatory.
    //IMod is API2, that part is necessary to access API2 stuff, add it and Alt+Enter and the default functions should show up
    //ModInterface is API1, that part is necessary to access API1 stuff, add it and Alt+Enter and the default functions should show up
    public class MyEmpyrionMod : IMod, ModInterface
    {

        //These 2 variables so you can access the API functions later
        internal static IModApi modApi2;
        internal static ModGameAPI modApi1;

        //This List is so we can do "If AdminList.Contains(SteamID)..."
        List<string> AdminList = new List<string> { };

        //This variable is to hold the AdminConfig data
        internal static AdminConfig.Root AdminConfigData = new AdminConfig.Root { };

        //Some way to track the last Sequence Number you used, note that ushort has a limit of numbers
        ushort NewSequenceNumber = 0;

        //To store SeqNr so you can call up all the data in your chain
        Dictionary<ushort, StorableData> SeqNrStorage = new Dictionary<ushort, StorableData> { };

        //A Class to store all the received data for when you have to chain requests to get all the data you need
        internal class StorableData
        {

            //TriggeringPlayer: I use for storing PlayerIDs
            public int TriggeringPlayer;

            //ChatInfo stores all of the ChatInfo from when a player said something that triggered the mod
            public ChatInfo ChatInfo;
        }


        //#######################################################################################################################################################
        //############################################################ API1 Starts Here #########################################################################
        //#######################################################################################################################################################


        //Required Function for API1
        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {

            //"If we received a chat message"
            if(eventId == CmdId.Event_ChatMessage)
            {

                //get the ChatInfo to a point where we can access it
                ChatInfo Received_ChatInfo = (ChatInfo)data;

                //"if message startswith /say"
                /*
                if (Received_ChatInfo.msg.StartsWith("/say "))
                {

                    //create a StorableData because we are going to need to request PlayerInfo to see if the player is an Admin
                    StorableData newStorable = new StorableData
                    {
                        TriggeringPlayer = Received_ChatInfo.playerId,
                        ChatInfo = Received_ChatInfo
                    };

                    //Increment the NewSequenceNumber variable so we get an unused number
                    NewSequenceNumber++;

                    //Store the StoreableData as the NewSequenceNumber so we have it for later
                    SeqNrStorage[NewSequenceNumber] = newStorable;

                    //API1 request the PlayerInfo for the TriggeringPlayer using the NewSequenceNumber so we can recall the ChatInfo later
                    modApi1.Game_Request(CmdId.Request_Player_Info, (ushort)NewSequenceNumber, new Id(Received_ChatInfo.playerId));
                }
                */
            }

            //"If we receive PlayerInfo"
            else if (eventId == CmdId.Event_Player_Info)
            {

                //get the PlayerInfo to a point where we can access it
                PlayerInfo Received_PlayerInfo = (PlayerInfo)data;

                //"if SeqNrStorage contains the SeqNr we just received..."
                if (SeqNrStorage.ContainsKey(seqNr))
                {

                    //Retrieve the data we stored under that SeqNr
                    StorableData RetrievedData = SeqNrStorage[seqNr];

                    //"if the PlayerInfo we received matches the player that triggered the mod AND they are not a regular player
                    //permission 3 is GM, 6 is Moderator, 9 is Admin, Player is 0 (I think)
                    if ( SeqNrStorage[seqNr].TriggeringPlayer == Received_PlayerInfo.entityId && Received_PlayerInfo.permission > 1)
                    {

                        //Try statement in case we cause an error here
                        try
                        {

                            //we already retrieved the data so we do not need to keep storing it, lets free upt he SeqNr for later
                            SeqNrStorage.Remove(seqNr);

                            //Split the chat message on Spaces
                            List<string> Restring = new List<string>(RetrievedData.ChatInfo.msg.Split(' '));

                            //remove the "/say"
                            Restring.Remove(Restring[0]);

                            //put the chat message back together
                            string Message = string.Join(" ", Restring.ToArray());

                            //we are going to be using a "Telnet" command for this next part, format the string for that purpose
                            string ConsoleCommand = "say '" + Message + "'";

                            //it says "Console Command" but its really a "Telnet" command, we dont need to store this SeqNr for later. This line is sending the request for the server to repeat what the Admin said after /say
                            modApi1.Game_Request(CmdId.Request_ConsoleCommand, (ushort)NewSequenceNumber, new PString(ConsoleCommand));
                        }
                        //The other part of the Try statement, it catches errors so you can log them.
                        catch { }
                    }
                }
            }
        }

        //required by API1, though we arent using it for anything in this mod
        public void Game_Exit()
        {
            //API1 version of Shutdown
        }

        //required by API1
        public void Game_Start(ModGameAPI dediAPI)
        {
            //API1 version of Init

            //store the ModGameAPI as modApi1 so we can use those functions later
            modApi1 = dediAPI;
        }

        //required by API1, though we arent using it for anything in this mod. In fact, I think Eleon broke this one...
        public void Game_Update()
        {
            //API1 version of Application_Update (not shown here)
        }


        //#######################################################################################################################################################
        //############################################################ API2 Starts Here #########################################################################
        //#######################################################################################################################################################

        //Required by API2, Init is run when the server starts
        public void Init(IModApi modAPI)
        {
            //API2 version of Game_Start

            //Store the IModApi in a variable so we can access it later
            modApi2 = modAPI;

            //many of the pieces of API2 don't run on all the process types so we are doing an if statement to have the Chatmessage testing only run on the DedicatedServer process
            if (modApi2.Application.Mode == ApplicationMode.DedicatedServer)
            {

                //set up a Listener for Chat Messages
                modApi2.Application.ChatMessageSent += Application_ChatMessageSent;
            }

            //Read the AdminConfig.yaml into a variable
            AdminConfigData = AdminConfig.ReadYaml("..\\Saves\\adminconfig.yaml");

            //add all the SteamIDs to a list so we can check the list contains the speaker later
            foreach (AdminConfig.elevated Admin in AdminConfigData.Elevated)
            {
                AdminList.Add(Admin.Id);
            }
        }


        //Required because we set up a Listerner for chat messages
        private void Application_ChatMessageSent(MessageData chatMsgData)
        {
            //the API2 version

            //Take the PlayerID and convert it to a SteamID
            string SteamID = modApi2.Application.GetPlayerDataFor(chatMsgData.SenderEntityId).Value.SteamId;

            //For this mod we are requiring the Admin say the message in the "Server Chat Window" AND we are also checking that the speaker is an Admin
            if (chatMsgData.Text.StartsWith("/say ") && AdminList.Contains(SteamID))
            {
                string[] SplitMsg = chatMsgData.Text.Split(' ') ;
                string msg = "";
                for (int i =1; i<SplitMsg.Count(); i++)
                {
                    msg = msg + " " + SplitMsg[i];
                }
                //Complicated thing for assembling all the variables for Sending a Server message
                MessageData SendableMsgData = new MessageData
                {
                    //Channel = Eleon.MsgChannel.SinglePlayer,
                    Channel = Eleon.MsgChannel.Global,
                    //RecipientEntityId = chatMsgData.SenderEntityId,
                    Text = msg,
                    SenderNameOverride = modApi2.Application.GetPlayerDataFor(chatMsgData.SenderEntityId).Value.PlayerName,
                    SenderType = Eleon.SenderType.ServerPrio
                };

                //Send the request to the server
                modApi2.Application.SendChatMessage(SendableMsgData);
            }
        }

        //Required by API2
        public void Shutdown()
        {
            //API2 version of Game_Exit
        }
    }
}
