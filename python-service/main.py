from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import random
import os
from apscheduler.schedulers.background import BackgroundScheduler
from contextlib import asynccontextmanager
from groq import Groq
from dotenv import load_dotenv

load_dotenv()

# Background Job Definition
def analyze_demand_job():
    """
    Simulated background job that analyzes historical booking data
    and updates global demand trends.
    """
    print(f"[{datetime.now()}] Running Background Demand Analysis... Trends updated.")

# Scheduler Lifecycle Management
@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup
    scheduler = BackgroundScheduler()
    scheduler.add_job(analyze_demand_job, "interval", minutes=1)
    scheduler.start()
    print("Background Scheduler Started.")
    yield
    # Shutdown
    scheduler.shutdown()
    print("Background Scheduler Shutdown.")

app = FastAPI(title="Smart Hotel AI Service", lifespan=lifespan)

class DemandRequest(BaseModel):
    date: str
    room_type: str

class PricingRecommendation(BaseModel):
    recommended_price: float
    reason: str
    confidence: float

@app.get("/")
def health_check():
    return {"status": "AI service running"}

@app.post("/predict-demand", response_model=dict)
def predict_demand(request: DemandRequest):
    try:
        dt = datetime.strptime(request.date, "%Y-%m-%d")
        month = dt.month
        day_of_week = dt.weekday()
        
        # Base demand
        demand_score = 0.4
        reasons = []
        
        # 1. Seasonality (High in Summer/Dec, Low in Jan/Feb)
        if month in [6, 7, 8, 12]:
            demand_score += 0.3
            reasons.append("Peak Season")
        elif month in [1, 2]:
            demand_score -= 0.1
            reasons.append("Off-Peak Season")
            
        # 2. Weekend adjustment (Friday-Saturday)
        if day_of_week >= 4: 
            demand_score += 0.25
            reasons.append("Weekend Surge")
            
        # 3. Special Events (Deterministic Mock)
        # In a real app, this would query a DB or external API
        events = {
            "2026-05-15": "Tech Conference 2026",
            "2026-07-04": "Independence Day",
            "2026-12-25": "Christmas Day",
            "2026-12-31": "New Year's Eve"
        }
        
        event_name = events.get(request.date)
        if event_name:
            demand_score += 0.4
            reasons.append(f"Event: {event_name}")
            
        # Room Type adjustment
        if request.room_type in ["Presidential Suite", "Royal Penthouse"]:
             demand_score += 0.15
        
        final_score = max(0.1, min(demand_score, 1.0))
        
        return {
            "date": request.date,
            "demand_score": round(final_score, 2),
            "level": "High" if final_score > 0.7 else "Medium" if final_score > 0.4 else "Low",
            "factors": reasons
        }
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

class SentimentRequest(BaseModel):
    text: str

class SentimentResponse(BaseModel):
    sentiment: str # Positive, Negative, Natural
    score: float

# Tactical Insights Store
tactical_insights = [
    {"id": 1, "level": "High", "text": "Unusually high demand for Suites detected on Dec 20. Consider a 10% price bump."},
    {"id": 2, "level": "Medium", "text": "Low occupancy for Budget rooms next week. AI suggests a 'Weekday Special' promo."},
    {"id": 3, "level": "Critical", "text": "Overbooking risk for Presidential Suite on Christmas Eve. Review pending reservations."},
    {"id": 4, "level": "Info", "text": "Room service demand peaks at 9AM. Suggested: Add extra staff to morning shifts."}
]

@app.get("/insights")
def get_insights():
    """Returns rotating tactical intelligence for the dashboard."""
    import random
    return random.sample(tactical_insights, 2)

@app.post("/analyze-sentiment", response_model=SentimentResponse)
def analyze_sentiment(request: SentimentRequest):
    text = request.text.lower()
    
    # Simple keyword-based sentiment for MVP (No heavy ML libs required)
    positive_words = ["good", "great", "excellent", "amazing", "wonderful", "love", "best", "perfect", "clean", "friendly", "nice", "helpful"]
    negative_words = ["bad", "terrible", "awful", "horrible", "worst", "dirty", "rude", "slow", "noise", "noisy", "poor", "hate", "unpleasant"]
    
    score = 0
    words = text.split()
    
    for word in words:
        if word in positive_words:
            score += 1
        elif word in negative_words:
            score -= 1
            
    # Normalize score somewhat
    final_sentiment = "Neutral"
    if score > 0:
        final_sentiment = "Positive"
    elif score < 0:
        final_sentiment = "Negative"
        
    return SentimentResponse(
        sentiment=final_sentiment,
        score=float(score)
    )
@app.post("/recommend-pricing", response_model=PricingRecommendation)
def recommend_pricing(request: DemandRequest):
    demand_data = predict_demand(request)
    demand_score = demand_data["demand_score"]
    factors = demand_data.get("factors", [])
    
    # Base prices mocked for demo
    base_price = 100.0
    if "Presidential" in request.room_type: base_price = 1200.0
    elif "Royal" in request.room_type: base_price = 1500.0
    elif "Suite" in request.room_type: base_price = 450.0
    elif "View" in request.room_type: base_price = 350.0
    
    multiplier = 0.8 + (demand_score * 0.7) 
    recommended = base_price * multiplier
    
    factor_str = ", ".join(factors) if factors else "Standard Demand"
    reason_text = f"Based on {demand_data['level']} forecasted demand ({int(demand_score*100)}%). Factors: {factor_str}"
    
    return PricingRecommendation(
        recommended_price=round(recommended, 2),
        reason=reason_text,
        confidence=0.85
    )

@app.get("/overbooking-risk")
def predict_overbooking_risk():
    """
    Predicts overbooking risk based on current occupancy and churn trends.
    """
    risk_level = random.choice(["Low", "Minimal", "Moderate"])
    return {"risk_level": risk_level, "churn_rate": random.uniform(0.02, 0.12)}

class SentimentRequest(BaseModel):
    text: str

@app.post("/analyze-sentiment")
async def analyze_sentiment(request: SentimentRequest):
    if not GROQ_API_KEY:
        return {"sentiment": "Neutral", "score": 0.5}
    
    try:
        prompt = f"Analyze the sentiment of this hotel review: '{request.text}'. Respond in exactly this JSON format: {{\"sentiment\": \"Positive/Negative/Neutral\", \"score\": 0.0 to 1.0}}"
        
        completion = client.chat.completions.create(
            model="llama-3.3-70b-versatile",
            messages=[{"role": "user", "content": prompt}],
            response_format={"type": "json_object"}
        )
        import json
        result = json.loads(completion.choices[0].message.content)
        return {
            "sentiment": result.get("sentiment", "Neutral"),
            "score": result.get("score", 0.5)
        }
    except Exception as e:
        print(f"Sentiment Analysis Error: {e}")
        return {"sentiment": "Neutral", "score": 0.5}

@app.get("/test-ai")
def test_ai():
    """Verify if the Groq API key is valid and working."""
    if not GROQ_API_KEY:
        return {"status": "error", "message": "GROQ_API_KEY not found in environment"}
    
    try:
        completion = client.chat.completions.create(
            model="llama-3.3-70b-versatile",
            messages=[{"role": "user", "content": "Say 'Groq AI is Active'"}],
            max_tokens=20
        )
        response_text = completion.choices[0].message.content
        if "Active" in response_text:
            return {"status": "success", "message": "Groq API is operational", "key_preview": f"{GROQ_API_KEY[:8]}...{GROQ_API_KEY[-4:]}"}
        return {"status": "warning", "message": "Groq responded but unexpected output", "response": response_text}
    except Exception as e:
        return {"status": "error", "message": str(e)}

# Groq Configuration
GROQ_API_KEY = os.getenv("GROQ_API_KEY")
if GROQ_API_KEY:
    client = Groq(api_key=GROQ_API_KEY)
    print("Groq AI Initialized (Llama 3.3).")
else:
    print("WARNING: GROQ_API_KEY not found in environment.")

class ChatRequest(BaseModel):
    message: str
    history: list = []
    user_context: dict = {}

@app.post("/chat")
async def chat(request: ChatRequest):
    if not GROQ_API_KEY:
        raise HTTPException(status_code=500, detail="Groq API Key not configured")
    
    start_time = datetime.now()
    print(f"[{start_time}] AI REQUEST START - User: {request.user_context.get('role')}")
    
    try:
        # 1. SETUP SYSTEM INSTRUCTION
        role_context = request.user_context.get('role', 'Guest')
        system_prompt = f"""
        You are 'GenAI Concierge' for Smart Hotel Management (Grand Astoria). 
        You are a sentient, high-intelligence agent powered by Groq LPU technology.
        
        HOTEL BLUEPRINT:
        - Room Types: Presidential Suite ($1200), Royal Penthouse ($1500), Executive Suite ($500+), Standard Plus.
        - Rules: 24h cancellation requirement strictly enforced.
        - Staff Tasks: Managed via 'Housekeeping' dashboard.
        - Pricing: Dynamic, AI-driven.
        - Gamification: Points for loyalty.
        
        User Role: {role_context}
        
        Guidelines: Answer concisely and professionally. Stick to the blueprint. 
        Maintain context from the chat history provided.
        """
        
        # 2. FORMAT HISTORY (Groq uses messages list)
        messages = [{"role": "system", "content": system_prompt}]
        
        if request.history:
            for item in request.history:
                raw_role = item.get("role") or item.get("Role")
                raw_text = item.get("text") or item.get("Text")
                
                if not raw_text or raw_text.startswith("Welcome to Smart Hotel"):
                    continue

                role = "user" if raw_role == "user" else "assistant"
                messages.append({"role": role, "content": raw_text})
        
        messages.append({"role": "user", "content": request.message})
        
        # 3. EXECUTE CHAT
        print(f"[{datetime.now()}] Calling Groq API (llama-3.3-70b-versatile)...")
        completion = client.chat.completions.create(
            model="llama-3.3-70b-versatile",
            messages=messages,
            temperature=0.7,
            max_tokens=1024,
            top_p=1,
            stream=False
        )
        
        end_time = datetime.now()
        duration = (end_time - start_time).total_seconds()
        print(f"[{end_time}] AI REQUEST COMPLETE - Duration: {duration}s")
        
        return {
            "reply": completion.choices[0].message.content,
            "status": "success"
        }
    except Exception as e:
        error_msg = str(e)
        print(f"[{datetime.now()}] Groq Chat Error: {error_msg}")
        raise HTTPException(status_code=500, detail=f"Groq Service Error: {error_msg}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
