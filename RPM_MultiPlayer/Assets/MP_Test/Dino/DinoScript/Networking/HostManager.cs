using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostManager : MonoBehaviour
{
    // 싱글 톤

    [Header("Settings")] 
    [SerializeField] private int maxConnections = 4;
    [SerializeField] private string characterSelectSceneName = "CharacterSelect";
    [SerializeField] private string gameplaySceneName = "GamePlay";
    
    public static HostManager Instance { get; private set; }

    private bool gameHasStarted;

    private string lobbyId;
    
    public Dictionary<ulong,ClientData> ClientData { get; private set; }
    public string JoinCode { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public async void StartHost()
    {
        Allocation allocation;

        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections); 
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay create allocation request failed {e.Message}");
            throw;
        }

        Debug.Log($"server : {allocation.ConnectionData[0]} {allocation.ConnectionData[1]}");
        Debug.Log($"server : {allocation.AllocationId}");

        try
        {
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch
        {
            Debug.LogError("Relay get join code request failed");
            throw;
        }

        var relayServerData = new RelayServerData(allocation, "dtls");
        
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        //lobby
        try
        {
            var createLobbyOptions = new CreateLobbyOptions();
            createLobbyOptions.IsPrivate = false;
            createLobbyOptions.Data = new Dictionary<string, DataObject>()
            {
                {
                    "JoinCode", new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: JoinCode)
                }
            };

            Lobby lobby = await Lobbies.Instance.CreateLobbyAsync("MyLobby", maxConnections, createLobbyOptions);
            lobbyId = lobby.Id;
            StartCoroutine(HeartBeatLobbyCoroutine(15));
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
            throw;
        }

        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted += OnNetworkReady;

        ClientData = new Dictionary<ulong, ClientData>();
        
        NetworkManager.Singleton.StartHost();
    }

    private IEnumerator HeartBeatLobbyCoroutine(float waitTimeSeconds)
    {
        var delay = new WaitForSeconds(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest arg1, NetworkManager.ConnectionApprovalResponse arg2)
    {
        if (ClientData.Count >= 3 || gameHasStarted)
        {
            // Approval Check 입증 시 실패 조건
            arg2.Approved = false;
            return;
        }

        arg2.Approved = true;
        arg2.CreatePlayerObject = false;
        arg2.Pending = false;

        // 조건에 충족하면 접속한 Client ID 로 Client 객체를 만든다.
        ClientData[arg1.ClientNetworkId] = new ClientData(arg1.ClientNetworkId);
        Debug.Log($"add client {arg1.ClientNetworkId} and sum of the Client is {ClientData.Count} and {ClientData.Keys}");}
    
    
    private void OnNetworkReady()
    {
        // 서버에서 클라이언트가 서버에서 연결을 끊었을 때마다 호출되는 이벤트
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        
        // 캐릭터 Select 씬으로 넘어감
        NetworkManager.Singleton.SceneManager.LoadScene(characterSelectSceneName, LoadSceneMode.Single);
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (ClientData.ContainsKey(clientId))
        {
            if(ClientData.Remove(clientId))
                Debug.Log($"Remove client {clientId}");
        }
    }

    public void SetCharacter(ulong Clientid, int CharacterId)
    {
        /*
         * TryGetValue(TKey key, out TValue value);
         * key: 찾고자 하는 요소의 키
         * value : key 에 맞는 value 값이 잆으면 ClinetData 데이터 형식으로 가져오고, 찾지 못하면 기본값으로 설정
         */
        if (ClientData.TryGetValue(Clientid, out ClientData data))
        {
            data.characterId = CharacterId;
        }
        
    }

    public void StartGame()
    {
        gameHasStarted = true;

        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}