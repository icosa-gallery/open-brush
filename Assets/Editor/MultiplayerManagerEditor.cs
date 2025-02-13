// MultiplayerManagerInspector.cs
using UnityEditor;
using UnityEngine;
using OpenBrush.Multiplayer;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

#if UNITY_EDITOR
[CustomEditor(typeof(MultiplayerManager))]
public class MultiplayerManagerInspector : Editor
{
    private MultiplayerManager multiplayerManager;
    private string roomName = "1234";
    private string nickname = "PlayerNickname";
    private string oldNickname = "PlayerNickname";
    private bool isPrivate = false;
    private int maxPlayers = 4;
    private bool voiceDisabled = false;

    public override void OnInspectorGUI()
    {
        multiplayerManager = (MultiplayerManager)target;

        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(10);


        roomName = EditorGUILayout.TextField("Room Name", roomName);
        nickname = EditorGUILayout.TextField("Nickname", nickname);
        if (nickname != oldNickname)
        {
            SetNickname();
            oldNickname = nickname;
            EditorUtility.SetDirty(target);
        }
        maxPlayers = EditorGUILayout.IntField("MaxPlayers", maxPlayers);

        //State
        string connectionState = "";
        if (multiplayerManager != null) connectionState = multiplayerManager.State.ToString();
        else connectionState = "Not Assigned";

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Connection State: ", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{connectionState}");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        if (GUILayout.Button("Connect"))
        {
            ConnectToLobby();
            EditorUtility.SetDirty(target);
        }


        if (GUILayout.Button("Join Room"))
        {
            ConnectToRoom();
            EditorUtility.SetDirty(target);
        }


        if (GUILayout.Button("Exit Room"))
        {
            DisconnectFromRoom();
            EditorUtility.SetDirty(target);
        }


        if (GUILayout.Button("Disconnect"))
        {
            Disconnect();
            EditorUtility.SetDirty(target);
        }

        if (GUILayout.Button("Refresh Room List"))
        {
            CheckIfRoomExists();
            EditorUtility.SetDirty(target);
        }

        //Local Player Id
        string localPlayerId = "";
        if (multiplayerManager.m_LocalPlayer != null) localPlayerId = multiplayerManager.m_LocalPlayer.PlayerId.ToString();
        else localPlayerId = "Not Assigned";

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Local Player ID: ", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{localPlayerId}");
        EditorGUILayout.EndHorizontal();

        //Room Ownership
        string ownership = "";
        if (multiplayerManager != null && multiplayerManager.IsUserRoomOwner()) ownership = "Yes";
        else if (multiplayerManager != null && !multiplayerManager.IsUserRoomOwner()) ownership = "No";
        else ownership = "Not Assigned";

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Is Local Player Room Owner:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{ownership}");
        EditorGUILayout.EndHorizontal();

        //Remote Users
        string remoteUsersRegistered = "";
        if (multiplayerManager.m_RemotePlayers != null && multiplayerManager.m_RemotePlayers.List.Count > 0)
        {
            remoteUsersRegistered = "UserIds:[ ";
            foreach (var remotePlayer in multiplayerManager.m_RemotePlayers.List)
            {
                remoteUsersRegistered += remotePlayer.PlayerId.ToString() + ",";
            }
            remoteUsersRegistered += "]";
        }
        else remoteUsersRegistered = "Not Assigned";

        //Registered remote players

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Registered Remote Players IDs:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{remoteUsersRegistered}");
        EditorGUILayout.EndHorizontal();



        Repaint();

    }

    private async void ConnectToLobby()
    {
        if (multiplayerManager != null)
        {
            bool success = await multiplayerManager.Connect();
        }
    }

    private async void ConnectToRoom()
    {
        if (multiplayerManager != null)
        {
            RoomCreateData roomData = new RoomCreateData
            {
                roomName = roomName,
                @private = isPrivate,
                maxPlayers = maxPlayers,
                voiceDisabled = voiceDisabled
            };

            bool success = await multiplayerManager.JoinRoom(roomData);

        }
    }

    private async void SetNickname()
    {
        if (multiplayerManager != null)
        {

            ConnectionUserInfo ui = new ConnectionUserInfo
            {
                Nickname = nickname,
                UserId = MultiplayerManager.m_Instance.UserInfo.UserId,
                Role = MultiplayerManager.m_Instance.UserInfo.Role
            };
            MultiplayerManager.m_Instance.UserInfo = ui;
        }
    }

    private async void DisconnectFromRoom()
    {
        if (multiplayerManager != null)
        {
            bool success = await multiplayerManager.LeaveRoom();

        }
    }

    private async void Disconnect()
    {
        if (multiplayerManager != null)
        {
            bool success = await multiplayerManager.Disconnect();

        }
    }

    private void CheckIfRoomExists()
    {
        if (multiplayerManager != null)
        {
            bool roomExists = multiplayerManager.DoesRoomNameExist(roomName);
        }
    }
}
#endif
