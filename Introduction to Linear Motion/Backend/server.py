import uuid
import json
import asyncio
import logging
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware

# Configure rich, beautiful logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)s | %(message)s",
    datefmt="%H:%M:%S"
)
logger = logging.getLogger("IntroBackend")

app = FastAPI(
    title="NCERT Motion Simulation (Distance vs Displacement) Backend",
    description="WebSocket & HTTP server for NCERT Distance vs Displacement simulation.",
    version="1.0.0"
)

# Enable CORS to allow Unity WebGL and Editor builds to connect seamlessly
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
def read_root():
    return {"message": "NCERT Motion (Distance vs Displacement) Simulation Backend is running!"}

@app.get("/ws-info")
def get_ws_info():
    """
    Unity calls this endpoint first to discover the WebSocket URL.
    """
    logger.info("Unity requested WebSocket connection details via /ws-info")
    return {
        "websocket_url": "ws://127.0.0.1:8088/ws/lesson",
        "note": "NCERT Motion (Distance vs Displacement) lesson discovery endpoint"
    }

@app.websocket("/ws/lesson")
async def websocket_endpoint(websocket: WebSocket):
    await websocket.accept()
    logger.info("WebSocket connection established with Unity client!")
    
    session_id = str(uuid.uuid4())
    logger.info(f"Generated new session ID: {session_id}")

    try:
        while True:
            # Wait for messages from Unity
            data = await websocket.receive_text()
            try:
                message = json.loads(data)
                event_type = message.get("event")
                logger.info(f"Incoming Event: '{event_type}'")

                if event_type == "start_lesson":
                    logger.info("Processing 'start_lesson' request from Unity...")
                    logger.info(f"Student ID: {message.get('student_id')}")
                    logger.info(f"Subject Code: {message.get('subject_code')}")
                    logger.info(f"Topic Code: {message.get('topic_code')}")

                    # 1. Send 'scene_preload' event
                    preload_event = {
                        "event": "scene_preload",
                        "session_id": session_id
                    }
                    logger.info("Sending 'scene_preload' to Unity...")
                    await websocket.send_text(json.dumps(preload_event))
                    await asyncio.sleep(0.5)  # Small delay for robust network streaming

                    # 2. Send 'manifest' event containing Distance vs Displacement lesson variables
                    # Athlete Speed: 7.5 m/s, Journey Distance: 100.0m
                    manifest_event = {
                        "event": "manifest",
                        "session_id": session_id,
                        "manifest": {
                            "lesson_title": "NCERT Physics - Distance vs Displacement",
                            "journey_distance": 100.0,
                            "cycling_speed": 7.5
                        }
                    }
                    logger.info("Sending lesson 'manifest' variables (Distance: 100.0m, Athlete Speed: 7.5m/s)...")
                    await websocket.send_text(json.dumps(manifest_event))
                    await asyncio.sleep(0.5)

                    # 3. Send 'done' event to signal completion of stream
                    done_event = {
                        "event": "done",
                        "session_id": session_id
                    }
                    logger.info("Sending 'done' to complete setup stream...")
                    await websocket.send_text(json.dumps(done_event))

                elif event_type == "telemetry":
                    events = message.get("events", [])
                    for item in events:
                        telemetry_type = item.get("type")
                        timestamp = item.get("timestamp")
                        detail = item.get("detail")
                        # Print VR head tracking, eye tracking, and control play/pause telemetry in real-time
                        logger.info(
                            f"[TELEMETRY] Type: {telemetry_type} | Time: {timestamp} | Detail: {detail}"
                        )

                else:
                    logger.warning(f"Unknown event type received: '{event_type}'")

            except json.JSONDecodeError:
                logger.error(f"Failed to parse JSON string: {data}")
            except Exception as ex:
                logger.error(f"Error handling message: {ex}")

    except WebSocketDisconnect:
        logger.info("Unity client disconnected from WebSocket.")
    except Exception as e:
        logger.error(f"WebSocket connection encountered an error: {e}")

if __name__ == "__main__":
    import uvicorn
    logger.info("Starting NCERT Motion (Distance vs Displacement) Simulation Backend Server on http://127.0.0.1:8088")
    uvicorn.run(app, host="127.0.0.1", port=8088)
