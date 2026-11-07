// src/profile/enums: the profile protocol's enums (content.h, message_type.h, open_status.h, prof_error.h,
// prof_request.h).

namespace XI.Profile
{
    // PS2: ContentsClass
    public enum Content : ushort
    {
        None = 0,
        FFXI = 1,
    }

    // sqPolMessageHeader Type
    public enum MessageType : byte
    {
        Plain = 0,
        FriendRequest = 1,
        FriendAccepted = 9,
        FriendDeclined = 10,
        FriendStatus = 31,
    }

    // PS2: OpenStat
    public enum OpenStatus : byte
    {
        Online = 0,
        Away = 1,
        Invisible = 4,
    }

    // ProfRecvAns error byte
    public enum ProfError : byte
    {
        None = 0x00,
        Refused = 0x01,
        NotFound = 0x7E,
    }

    public enum ProfRequest : ushort
    {
        StoreHandleNameList = 0x0008, // PS2: __sqPlayOnlineStoreHandleNameListCheck
        LoadHandleNameList = 0x0009,  // PS2: __sqPlayOnlineLoadHandleNameListCheck (PS2 0/7)
        LoadCharacterList = 0x0103,   // PS2: __sqPlayOnlineLoadCharacterListCheck
        StoreCharacterList = 0x010A,  // PS2: __sqPlayOnlineStoreCharacterListCheck
        SearchPolId = 0x010B,         // PS2: sqPlayOnlineSearchPolIdCheck2
        LoadFriendList = 0x0203,      // PS2: __sqPlayOnlineLoadFriendListCheck
        StoreFriendList = 0x0206,     // PS2: __sqPlayOnlineStoreFriendListCheck
        ReadFile = 0x0300,            // PS2: __sqProfReadFileCheck
        WriteFile = 0x0301,           // PS2: __sqProfWriteFileCheck
        RemoveFile = 0x0302,          // PS2: __sqProfDeleteFileCheck
        GetFileList = 0x0303,         // PS2: __sqProfGetFileListCheck
        WriteFileMulti = 0x0304,      // PS2: __sqProfWriteFileMultiCheck
        SetHandleComment = 0x0403,    // PS2: (New; did not exist.)
        ChangeMyStatus = 0x0405,      // PS2: sqPlayOnlineChangeMyStatusCheck (PS2 4/0)
        LoadMyStatus = 0x0406,        // PS2: sqPlayOnlineLoadMyStatusCheck (PS2 4/1)
        SecurityToken = 0x0407,       // PS2: (New; did not exist.)
        PolbesUpdateRecord = 0x0501,  // PS2: sqPolbesUpdateRecord
        PolbesSearchDataAll = 0x0503, // PS2: sqPolbesSearchDataAll
        PolbesSearchData = 0x0504,    // PS2: sqPolbesSearchData
        CreateGroup = 0x0701,         // PS2: (New; did not exist.)
        DeleteGroup = 0x0702,         // PS2: (New; did not exist.)
        ChangeGroupMember = 0x0703,   // PS2: (New; did not exist.)
        UpdateMyGroupEntry = 0x070B,  // PS2: (New; did not exist.)
        LoadGroupList = 0x070C,       // PS2: (New; did not exist.)
    }
}
