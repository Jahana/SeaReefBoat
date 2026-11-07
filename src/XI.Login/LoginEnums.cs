// C# port of the login-server (xiloader/auth + lobby) enum surface. Values verbatim
// from src/login/auth_session.h, login_errors.h, and login_helpers.h, and cross-checked
// against the compiled oracle dump (login_lobby_golden.txt) by tests/XI.Verify.Login.

using System;

namespace XI.Login
{
    // Interaction with xiloader, port 54231 (auth_session.h: enum class login_cmd : uint8_t).
    public enum LoginCmd : byte
    {
        LoginNoop = 0x00,
        LoginAttempt = 0x10,
        LoginCreate = 0x20,
        LoginChangePassword = 0x30,

        // json xiloader
        LoginCreateTotp = 0x31,
        LoginRemoveTotp = 0x32,
        LoginRegenerateRecovery = 0x33,
        LoginVerifyTotp = 0x34,
    }

    // auth_session.h: enum class login_result : uint8_t.
    public enum LoginResult : byte
    {
        LoginFail = 0x00,
        LoginSuccess = 0x01,
        LoginError = 0x02,
        LoginSuccessCreate = 0x03,
        LoginErrorCreateTaken = 0x04,
        LoginRequestNewPassword = 0x05,
        LoginSuccessChangePassword = 0x06,
        LoginErrorChangePassword = 0x07,
        LoginErrorCreateDisabled = 0x08,
        LoginErrorCreate = 0x09,
        LoginErrorAlreadyLoggedIn = 0x0A,
        LoginErrorVersionUnsupported = 0x0B,
        LoginSuccessCreateTotp = 0x10,
        LoginSuccessVerifyTotp = 0x11,
        LoginSuccessRemoveTotp = 0x12,
        LoginErrorTrustTokenInvalid = 0x13,
    }

    // auth_session.h: AUTH_COMPONENTS (NOTE: this flag set is 64-bits wide).
    [Flags]
    public enum AuthComponents : ulong
    {
        // Information to be sent during login sequence, appended after the mandatory
        // [username][password]: [username][password][email] when SEND_EMAIL is set.
        SendEmail = 1UL << 0,
        SendHostname = 1UL << 1,
        SendMacAddress = 1UL << 2,

        // Enable additional workflows between connect and xiloader.
        EnableAccountCreate = 1UL << 3,
        EnableAccountDelete = 1UL << 4,
        EnablePasswordChange = 1UL << 5,
        EnablePasswordReset = 1UL << 6,
    }

    // auth_session.h: enum ACCOUNT_STATUS_CODE : uint8.
    public enum AccountStatusCode : byte
    {
        Normal = 0x01,
        Banned = 0x02,
    }

    // auth_session.h: enum ACCOUNT_PRIVILEGE_CODE : uint8.
    public enum AccountPrivilegeCode : byte
    {
        User = 0x01,
        Admin = 0x02,
        Root = 0x04,
    }

    // auth_session.h: constexpr std::array<uint8,3> SupportedXiloaderVersion = {2,2,0}.
    public static class XiloaderVersion
    {
        public static readonly byte[] Supported = { 2, 2, 0 };
    }

    // login_errors.h: namespace loginErrors { enum errorCode : uint16_t }.
    public enum ErrorCode : ushort
    {
        UnableToConnectToWorldServer = 305,
        CharacterNameUnavailable = 313,
        CharacterAlreadyLoggedIn = 201,
        WorldIsFull = 208,
        FailedToRegisterWithTheNameServer = 314,
        CharactersParametersAreIncorrect = 321,
        GamesDataHasBeenUpdated = 331,
        CouldNotConnectToLobbyServer = 332,
    }

    // login_helpers.h: enum EXPANSION_DISPLAY : uint16 (main-menu expansion bits).
    [Flags]
    public enum ExpansionDisplay : ushort
    {
        BaseGame = 0x0001,          // not used by the client
        RiseOfZilart = 0x0002,
        ChainsOfPromathia = 0x0004,
        TreasuresOfAhtUrgan = 0x0008,
        WingsOfTheGoddess = 0x0010,
        ACrystallineProphecy = 0x0020,
        AMoogleKupodEtat = 0x0040,
        AShantottoAscension = 0x0080,
        VisionsOfAbyssea = 0x0100,
        ScarsOfAbyssea = 0x0200,
        HeroesOfAbyssea = 0x0400,
        SeekersOfAdoulin = 0x0800,
        UnusedExpansion1 = 0x1000,
        UnusedExpansion2 = 0x2000,
        UnusedExpansion3 = 0x4000,
        UnusedExpansion4 = 0x8000,
    }

    // login_helpers.h: enum FEATURE_DISPLAY : uint16 (main-menu feature bits).
    [Flags]
    public enum FeatureDisplay : ushort
    {
        SecureToken = 0x0001,
        UnusedFeature1 = 0x0002,
        MogWardrobe3 = 0x0004,
        MogWardrobe4 = 0x0008,
        MogWardrobe5 = 0x0010,
        MogWardrobe6 = 0x0020,
        MogWardrobe7 = 0x0040,
        MogWardrobe8 = 0x0080,
        UnusedFeature2 = 0x0100,
        UnusedFeature3 = 0x0200,
        UnusedFeature4 = 0x0400,
        UnusedFeature5 = 0x0800,
        UnusedFeature6 = 0x1000,
        UnusedFeature7 = 0x2000,
        UnusedFeature8 = 0x4000,
        UnusedFeature9 = 0x8000,
    }
}
