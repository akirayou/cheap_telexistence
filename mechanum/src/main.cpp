#include <Arduino.h>
#include <PSX.h>
#include "SPDMotor.h"
PSX psx;
//
const unsigned long  PID_span_ms =10;
const float PID_span=PID_span_ms/1000.0f;
const unsigned long PID_span_us=PID_span_ms*1000;
const float encoder_rate=1e+6f/2500.0f/2.5*0.5; //maxパワーのeSpeedをを1ちょっとにする
SPDMotor motorLF(18, 31, false, 12, 35, 34,encoder_rate,PID_span_us); 
SPDMotor motorRF(19, 38, true ,  8, 37, 36,encoder_rate,PID_span_us); 
SPDMotor motorLR( 3, 49, false,  6, 42, 43,encoder_rate,PID_span_us); 
SPDMotor motorRR( 2, 55, true ,  5, A4, A5,encoder_rate,PID_span_us); 
SPDMotor* motors[]={&motorLF,&motorRF,&motorLR,&motorRR};
void printPID(){
  Serial.print("P:");
  Serial.print(motorLF.k.P);
  Serial.print("I:");
  Serial.print(motorLF.k.I);
  Serial.print("D:");
  Serial.println(motorLF.k.D);
}
void setP(float p){
  for(byte i=0;i<4;i++)motors[i]->k.P=p;
  printPID();
}
void setI(float p){
  for(byte i=0;i<4;i++)motors[i]->k.I=p;
  printPID();
}
void setD(float p){
  for(byte i=0;i<4;i++)motors[i]->k.D=p;
  printPID();
}

void setup() {
  Serial.begin(115200);
  setP(500);
  setI(100);
  setD(100);
  for(byte i=0;i<4;i++)motors[i]->setEmode(true);
 //Setup the PSX library
  delay(30);
  psx.setupPins(52/*DAT*/, 51/*CMD*/, 53/*ATT*/, 50/*CLK*/, 10/*delay us for cmd*/);
  psx.config(PSXMODE_ANALOG);
}

struct {
  float forward;
  float right;
  float rot_cw;
}vel;
float joiy_normalize(byte x ){
  if(127==x || 128==x)return 0;//joyconセンターが127だったり128だったりする
  return (x-127.5f)/127.0f;
}
void applyFilter(float &org,float input,float rate=0.3){
  float diff=input-org;
  if(rate<diff)diff=rate;
  if(diff<-rate)diff= -rate;
  org+=diff;
}

void psx_loop(bool enable){
  
  int PSXerror;
  PSX::PSXDATA PSXdata;
  PSXerror = psx.read(PSXdata);
  if(!enable)return;//dummy read only
  if( (PSXerror == PSXERROR_SUCCESS) && (PSXdata.type ==0x73)) {
    float rate=0.5;
    if(PSXdata.buttons & PSXBTN_R1) {
      rate=0.25;
    }
    if(PSXdata.buttons & PSXBTN_R2) {
      rate=0.1;
    }
    applyFilter(vel.forward,rate*joiy_normalize (PSXdata.JoyLeftY ));
    applyFilter(vel.right  ,rate*joiy_normalize (PSXdata.JoyLeftX ));
    applyFilter(vel.rot_cw ,rate*joiy_normalize (PSXdata.JoyRightX));
  } else {
     applyFilter(vel.forward,0);
     applyFilter(vel.right  ,0);
     applyFilter(vel.rot_cw ,0);
    //Serial.println("No success reading data. Check connections and timing.");
  }
}
bool enableMotorMonitor=false;
unsigned long motorMonitorSpan=1000;
void set_speed(){
  static unsigned long lastDisp=0;
  unsigned long now=millis();

  float lf,rf,lr,rr;
  lf=rf=lr=rr=vel.forward;
  float r=vel.right*1.4142;
  lf+=r;
  rf-=r;
  lr-=r;
  rr+=r;
  lf+=vel.rot_cw;
  rf-=vel.rot_cw;
  lr+=vel.rot_cw;
  rr-=vel.rot_cw;
  float max_v=max(abs(lf), max(abs(rf), max(abs(lr),abs(rr)  )));
  if(max_v>1){
    lf/=max_v;
    rf/=max_v;
    lr/=max_v;
    rr/=max_v;
  }
  if(enableMotorMonitor &&  (now-lastDisp) >motorMonitorSpan){
    lastDisp=now;
    Serial.print(vel.forward);
    Serial.print("\t");
    Serial.print(vel.right);
    Serial.print("\t");
    Serial.print(vel.rot_cw);
    Serial.print("\t");
    
    Serial.print(lf,4);
    Serial.print(">>");Serial.print(motorLF.getESpeed(),4);
    Serial.print("\t");
    Serial.print(rf);
    Serial.print(">>");Serial.print(motorRF.getESpeed());
    Serial.print("\t");
    Serial.print(lr);
    Serial.print(">>");Serial.print(motorLR.getESpeed());
    Serial.print("\t");
    Serial.print(rr);
    Serial.print(">>");Serial.print(motorRR.getESpeed());
    Serial.println();

  }

  motorLF.eSpeed(lf);
  motorRF.eSpeed(rf);
  motorLR.eSpeed(lr);
  motorRR.eSpeed(rr);
}
long cmd_vel_count=0;
float cmd_lx=0;
float cmd_ly=0;
float cmd_rx=0;
bool enableContoroller=true;
void cmdDispatch(const String & cmd){
  int span=0;
  int tar_pos,tar_end;
  float lx,ly,rx;

  switch(cmd[0]){
    case 'm'://monitor ON
      enableMotorMonitor=true;
      span=cmd.substring(1).toInt();
      if(10<span && span<100000){
        motorMonitorSpan=span;
      }
      break;
    case 'M'://monitor Off
      enableMotorMonitor=false;
      break;
    case 'p':
      setP(cmd.substring(1).toFloat());
      break;
    case 'i':
      setI(cmd.substring(1).toFloat());
      break;
    case 'd':
      setD(cmd.substring(1).toFloat());
      break;
    case 's':
      printPID();
      Serial.print("Motor monitor span(ms):");
      Serial.println(motorMonitorSpan);
      Serial.print("Controller enable:");
      Serial.println(enableContoroller);
      Serial.print("last cmd vel lx,ly,rx:");
      Serial.print(cmd_lx);
      Serial.print(",");
      Serial.print(cmd_ly);
      Serial.print(",");
      Serial.println(cmd_rx);      
      break;
    case 'v':
      tar_pos=1;
      tar_end=cmd.indexOf(" ",tar_pos);
      if(tar_pos<0){
        Serial.println("Error:Parse");
        break;
      }
      lx=atof(cmd.substring(tar_pos,tar_end).c_str());
      tar_pos=tar_end+1;
      tar_end=cmd.indexOf(" ",tar_pos);
      if(tar_pos<0){
        Serial.println("Error:Parse");
        break;
      }
      ly=atof(cmd.substring(tar_pos,tar_end).c_str());
      tar_pos=tar_end+1;
      rx=atof(cmd.substring(tar_pos).c_str());
      if(lx<-1 || ly <-1 || rx<-1 || 1<lx || 1<ly ||1<rx){
        Serial.println("Error:Invalid range");
        break;
      }
      cmd_lx=lx;
      cmd_ly=ly;
      cmd_rx=rx;
      cmd_vel_count=500;
      break;
    case 'c':
      enableContoroller=true;
      break;
    case 'C':
      enableContoroller=false;
      break;
    default:
    Serial.println("m or m[ulong]: motor monitor ON and span(ms)");
    Serial.println("M: motor monitor Off");
    Serial.println("p[float]: PID param");
    Serial.println("i[float]: PID param");
    Serial.println("d[float]: PID param");
    Serial.println("v[float] [float] [float]: set forward/back  right/left rot");
    Serial.println("c: enable PS2 controller");
    Serial.println("C: disenable PS2 controller");
    Serial.println("s: print status");
  }

}
void cmdKick( long delta){
  if(cmd_vel_count<delta)cmd_vel_count=0;
  else cmd_vel_count-=delta;
  
  static String cmd="";
  int a=Serial.read();
  if(a==0x0a){
    Serial.println();
    cmdDispatch(cmd);
    cmd="";
  }
  else if(0x20<=a  && a<=0x7e){
    Serial.print((char)a);
    cmd+=(char)a;
  }
}


void loop() {
  static unsigned long old_millis=0;
  static unsigned long psx_delta=0;
  unsigned long now=millis();
  unsigned long delta=now-old_millis;
  old_millis=now;
  cmdKick(delta);
  psx_delta+=delta;

  if(10<psx_delta){

    psx_loop(cmd_vel_count==0 && enableContoroller);
    if(0<cmd_vel_count){
      applyFilter(vel.forward,cmd_lx);
      applyFilter(vel.right  ,cmd_ly);
      applyFilter(vel.rot_cw ,cmd_rx);
    }else if( ! enableContoroller){
      applyFilter(vel.forward,0);
      applyFilter(vel.right  ,0);
      applyFilter(vel.rot_cw ,0);
    }
    psx_delta=0;
  }

  set_speed();
  for(byte i=0;i<4;i++)motors[i]->kick();
}