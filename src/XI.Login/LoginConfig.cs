// The login server's settings as src/login reads them through settings::get<T> (settings/default/*.lua, then
// settings/*.lua, then the XI_<FILE>_<KEY> environment variables; XI.Common.Settings): one place that turns the
// settings into the ConnectConfig / AuthSettings / LobbySettings / ViewSettings the engine runs on, each key read with
// the type the C++ call site asks for, so a missing key is that type's T() and a string or number converts as the C++
// converts it.
//
//   key                            C++ call site                                     here
//   network.LOGIN_AUTH_PORT        connect_engine.cpp handler port (get<uint32>),    Connect.AuthPort, RoutingPort
//                                  getZMQRoutingId (get<uint16>)
//   network.LOGIN_DATA_PORT        connect_engine.cpp handler port (get<uint32>)     Connect.DataPort
//   network.LOGIN_VIEW_PORT        connect_engine.cpp handler port (get<uint32>)     Connect.ViewPort
//   network.LOGIN_AUTH_IP          getZMQRoutingId, cert_helpers.h self-signed CN    RoutingIp
//   network.ZMQ_TRANSPORT / ZMQ_IP / ZMQ_PORT   getZMQEndpointString                 ZmqEndpoint
//   network.SEARCH_PORT            data_session.cpp 0xA2 cache_port (get<uint16>)    Lobby.SearchPort
//   network.ZONE_IP_FROM_CLIENT    C# only: the 0x0B map address (ClientAddress)     Lobby.ZoneIpFromClient
//   main.SERVER_NAME               auth LOGIN_CREATE_TOTP uri, data 0xA1 list,       Auth/Lobby/View.ServerName
//                                  view 0x24 world list (get<std::string>)
//   main.NEW_CHARACTER_CUTSCENE    login_helpers.cpp createCharacter (get<bool>)     View.NewCharacterCutscene
//   login.ACCOUNT_CREATION         auth_session.cpp LOGIN_CREATE (get<bool>)         Auth.AccountCreation
//   login.MAINT_MODE               data 0xA1 (get<uint8>), data 0xA2 (get<bool>),    Lobby.MaintMode, Lobby.MaintModeOn,
//                                  characterCreationError (get<uint8>)               View.MaintMode
//   login.CHARACTER_CREATION       data 0xA1 empty slots (get<uint8>),               Lobby.CharacterCreation,
//                                  characterCreationError (get<bool>)                View.CharacterCreation
//   login.LOGIN_LIMIT              data 0xA2 (get<uint8>)                            Lobby.LoginLimit
//   login.LOG_USER_IP              data 0xA2 account_ip_record (get<bool>)           Lobby.LogUserIp
//   login.CHARACTER_DELETION       view 0x14 (get<bool>)                             View.CharacterDeletion
//   login.CLIENT_VER / VER_LOCK    view 0x26 (get<std::string> / get<uint8>)         View.ClientVer / VerLock
//   login.DISABLE_MOB_NPC_CHAR_NAMES  validateCharacterName, loadZoneLookups         View.DisableMobNpcCharNames
//   login.BANNED_WORDS_LIST        validateCharacterName (the Lua table)             View.BannedWords
//   login.<expansion> / MOG_WARDROBE_3..8   generateExpansion / FeatureBitmask       View.RiseOfZilart .. MogWardrobe8
//   map.ZONE_PLAYER_CAP / ZONE_PLAYER_GM_RESERVED   isZoneAtPlayerCap (get<uint16>)  Lobby.ZonePlayerCap / GmReserved

using System;
using XI.Common;

namespace XI.Login
{
    public sealed class LoginConfig
    {
        public ConnectConfig Connect;
        public AuthSettings Auth;
        public LobbySettings Lobby;
        public ViewSettings View;
        public string RoutingIp;     // network.LOGIN_AUTH_IP: the dealer's routing id is IPP(str2ip(ip), RoutingPort)
        public ushort RoutingPort;   // network.LOGIN_AUTH_PORT (get<uint16>)
        public string ZmqEndpoint;   // "<ZMQ_TRANSPORT>://<ZMQ_IP>:<ZMQ_PORT>"

        public static LoginConfig From(Settings s)
        {
            string serverName = s.Get<string>("main.SERVER_NAME");
            var c = new LoginConfig
            {
                // asio::ip::tcp::endpoint(v4(), port): the uint32 setting narrowed to the port's unsigned short
                Connect = new ConnectConfig
                {
                    AuthPort = (ushort)s.Get<uint>("network.LOGIN_AUTH_PORT"),
                    DataPort = (ushort)s.Get<uint>("network.LOGIN_DATA_PORT"),
                    ViewPort = (ushort)s.Get<uint>("network.LOGIN_VIEW_PORT"),
                },
                RoutingIp = s.Get<string>("network.LOGIN_AUTH_IP"),
                RoutingPort = s.Get<ushort>("network.LOGIN_AUTH_PORT"),
                ZmqEndpoint = $"{s.Get<string>("network.ZMQ_TRANSPORT")}://{s.Get<string>("network.ZMQ_IP")}:{s.Get<ushort>("network.ZMQ_PORT")}",
                Auth = new AuthSettings
                {
                    AccountCreation = s.Get<bool>("login.ACCOUNT_CREATION"),
                    ServerName = serverName,
                },
                Lobby = new LobbySettings
                {
                    MaintMode = s.Get<byte>("login.MAINT_MODE"),
                    MaintModeOn = s.Get<bool>("login.MAINT_MODE"),
                    CharacterCreation = s.Get<byte>("login.CHARACTER_CREATION") != 0,
                    LoginLimit = s.Get<byte>("login.LOGIN_LIMIT"),
                    LogUserIp = s.Get<bool>("login.LOG_USER_IP"),
                    ServerName = serverName,
                    SearchPort = s.Get<ushort>("network.SEARCH_PORT"),
                    ZoneIpFromClient = s.Get<bool>("network.ZONE_IP_FROM_CLIENT"),
                    ZonePlayerCap = s.Get<ushort>("map.ZONE_PLAYER_CAP"),
                    ZonePlayerGmReserved = s.Get<ushort>("map.ZONE_PLAYER_GM_RESERVED"),
                },
                View = new ViewSettings
                {
                    CharacterDeletion = s.Get<bool>("login.CHARACTER_DELETION"),
                    MaintMode = s.Get<byte>("login.MAINT_MODE"),
                    CharacterCreation = s.Get<bool>("login.CHARACTER_CREATION"),
                    ClientVer = s.Get<string>("login.CLIENT_VER"),
                    VerLock = s.Get<byte>("login.VER_LOCK"),
                    ServerName = serverName,
                    NewCharacterCutscene = s.Get<bool>("main.NEW_CHARACTER_CUTSCENE"),
                    DisableMobNpcCharNames = s.Get<bool>("login.DISABLE_MOB_NPC_CHAR_NAMES"),
                    // no table at all is no check; the C++ only walks a valid one
                    BannedWords = s.GetStringList("login.BANNED_WORDS_LIST") ?? Array.Empty<string>(),
                    RiseOfZilart = s.Get<bool>("login.RISE_OF_ZILART"),
                    ChainsOfPromathia = s.Get<bool>("login.CHAINS_OF_PROMATHIA"),
                    TreasuresOfAhtUrghan = s.Get<bool>("login.TREASURES_OF_AHT_URGHAN"),
                    WingsOfTheGoddess = s.Get<bool>("login.WINGS_OF_THE_GODDESS"),
                    ACrystallineProphecy = s.Get<bool>("login.A_CRYSTALLINE_PROPHECY"),
                    AMoogleKupodEtat = s.Get<bool>("login.A_MOOGLE_KUPOD_ETAT"),
                    AShantottoAscension = s.Get<bool>("login.A_SHANTOTTO_ASCENSION"),
                    VisionsOfAbyssea = s.Get<bool>("login.VISIONS_OF_ABYSSEA"),
                    ScarsOfAbyssea = s.Get<bool>("login.SCARS_OF_ABYSSEA"),
                    HeroesOfAbyssea = s.Get<bool>("login.HEROES_OF_ABYSSEA"),
                    SeekersOfAdoulin = s.Get<bool>("login.SEEKERS_OF_ADOULIN"),
                    MogWardrobe3 = s.Get<bool>("login.MOG_WARDROBE_3"),
                    MogWardrobe4 = s.Get<bool>("login.MOG_WARDROBE_4"),
                    MogWardrobe5 = s.Get<bool>("login.MOG_WARDROBE_5"),
                    MogWardrobe6 = s.Get<bool>("login.MOG_WARDROBE_6"),
                    MogWardrobe7 = s.Get<bool>("login.MOG_WARDROBE_7"),
                    MogWardrobe8 = s.Get<bool>("login.MOG_WARDROBE_8"),
                },
            };
            return c;
        }
    }
}
