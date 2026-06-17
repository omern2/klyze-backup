import requests
import json
from datetime import datetime

# Test Henrik Dev API timestamp parsing
api_key = "HDEV-06d4da7c-c8ae-446d-a653-9277e0ea7cb1"
base_url = "https://api.henrikdev.xyz"

# Test with the logged-in user
name = "DEV zynx"
tag = "Ezz"
region = "eu"

url = f"{base_url}/valorant/v3/matches/{region}/{name}/{tag}"
headers = {
    "Authorization": api_key,
    "Accept": "application/json"
}

print(f"Testing API call: {url}")
print(f"User: {name}#{tag} (Region: {region})")
print("-" * 50)

try:
    response = requests.get(url, headers=headers, timeout=10)
    print(f"Status Code: {response.status_code}")
    
    if response.status_code == 200:
        data = response.json()
        matches = data.get("data", [])
        print(f"Found {len(matches)} matches")
        
        if matches:
            # Check first few matches for timestamp data
            for i, match in enumerate(matches[:3]):
                metadata = match.get("metadata", {})
                game_start = metadata.get("game_start")
                map_name = metadata.get("map", "Unknown")
                
                print(f"\nMatch {i+1}:")
                print(f"  Map: {map_name}")
                print(f"  game_start (raw): {game_start}")
                
                if game_start:
                    try:
                        # Convert unix timestamp to readable date
                        dt = datetime.fromtimestamp(game_start)
                        print(f"  Date: {dt.strftime('%Y-%m-%d %H:%M:%S')}")
                        print(f"  ✓ Valid timestamp found!")
                    except Exception as e:
                        print(f"  ✗ Error parsing timestamp: {e}")
                else:
                    print(f"  ✗ No game_start timestamp found")
        else:
            print("No matches found in response")
    else:
        print(f"API Error: {response.status_code}")
        print(f"Response: {response.text}")
        
except Exception as e:
    print(f"Request failed: {e}")