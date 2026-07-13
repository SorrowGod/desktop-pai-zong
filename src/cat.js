import { loadConfig, saveConfig } from './config.js';

// 动画状态定义
const ANIM_STATES = {
  idle:    { row: 0, frames: 4, speed: 150 },
  walk:    { row: 1, frames: 6, speed: 100 },
  sleep:   { row: 2, frames: 4, speed: 400 },
  fall:    { row: 3, frames: 2, speed: 120, cols: [0, 1] },
  drag:    { row: 3, frames: 1, speed: 100, cols: [2] },
  reaction:{ row: 3, frames: 3, speed: 100, cols: [3, 4, 5] },
  happy:   { row: 4, frames: 4, speed: 120 },
  sad:     { row: 5, frames: 3, speed: 200 },
};

const FRAME_SIZE = 64;
const GRAVITY = 0.5;
const BOUNCE_DAMPING = 0.4;

const BUBBLE_MESSAGES = {
  happy: ['喵~', '开心~', '摸摸我~', '嘿嘿~', '喜欢你！', '喵呜~'],
  normal: ['喵', '无聊了...', '...', '在干嘛？', '理我一下嘛', '打个盹？'],
  sad: ['呜呜...', '不理我了...', '难过...', '想你了...', '哼！'],
  sleep: ['Zzz...', '...zZ', '好困...', '做梦中...'],
  reaction: ['哎呀！', '喵！', '别碰我~', '嘿嘿', '干嘛~'],
};

export class Cat {
  constructor(canvas, config) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.config = config;
    this.scale = config.petSize || 1;
    this.pinnedMode = !!config.pinnedMode;

    // 位置和物理
    this.x = canvas.width / 2;
    this.y = canvas.height - 24;
    this.vx = 0;
    this.vy = 0;
    this.groundY = canvas.height - 24;
    this.isFalling = false;
    this.isDragging = false;

    // 动画
    this.state = 'idle';
    this.frame = 0;
    this.frameTimer = 0;
    this.direction = 1; // 1=右, -1=左
    this.animState = ANIM_STATES.idle;

    // 行为
    this.behaviorTimer = 0;
    this.behaviorInterval = 3000 + Math.random() * 5000;
    this.walkTarget = null;

    // 情绪
    this.affection = config.affection || 50;
    this.lastInteraction = Date.now();
    this.bubbleText = '';
    this.bubbleTimer = 0;
    this.bubbleX = 0;
    this.bubbleY = 0;

    // 精灵图
    this.spriteSheet = null;
    this.spriteLoaded = false;

    this.loadSprite();
  }

  loadSprite() {
    this.spriteSheet = new Image();
    this.spriteSheet.onload = () => {
      this.spriteLoaded = true;
    };
    this.spriteSheet.onerror = () => {
      console.warn('Failed to load sprite sheet, using fallback');
      this.spriteLoaded = false;
    };
    this.spriteSheet.src = '/cat-sprites.png';
  }

  getMood() {
    if (this.state === 'sleep') return 'sleep';
    if (this.affection >= 70) return 'happy';
    if (this.affection < 30) return 'sad';
    return 'normal';
  }

  addAffection(amount) {
    this.affection = Math.max(0, Math.min(100, this.affection + amount));
    this.lastInteraction = Date.now();
    this.config.affection = this.affection;
    saveConfig(this.config);
  }

  setState(newState) {
    if (this.state === newState) return;
    this.state = newState;
    this.animState = ANIM_STATES[newState] || ANIM_STATES.idle;
    this.frame = 0;
    this.frameTimer = 0;
  }

  showBubble(text) {
    this.bubbleText = text || this.getRandomBubble();
    this.bubbleTimer = 120; // frames
    this.bubbleX = this.x;
    this.bubbleY = this.y - FRAME_SIZE * this.scale - 20;
  }

  getRandomBubble() {
    const mood = this.getMood();
    const messages = BUBBLE_MESSAGES[mood] || BUBBLE_MESSAGES.normal;
    return messages[Math.floor(Math.random() * messages.length)];
  }

  onClick() {
    this.setState('reaction');
    this.showBubble();
    this.addAffection(1);
    setTimeout(() => {
      if (this.state === 'reaction') this.setState('idle');
    }, 500);
  }

  onDragStart() {
    this.isDragging = true;
    this.isFalling = false;
    this.vx = 0;
    this.vy = 0;
    this.setState('drag');
  }

  onDragMove(x, y) {
    if (!this.isDragging) return;
    this.x = x;
    this.y = y;
  }

  onDragEnd() {
    if (!this.isDragging) return;
    this.isDragging = false;
    if (this.pinnedMode) {
      this.isFalling = false;
      this.vy = 0;
      this.setState('idle');
      return;
    }
    this.isFalling = true;
    this.vy = 0;
    this.setState('fall');
  }

  feed() {
    this.showBubble('好好吃~🐟');
    this.addAffection(5);
    this.setState('happy');
    setTimeout(() => { if (this.state === 'happy') this.setState('idle'); }, 1500);
  }

  pet() {
    this.showBubble('呼噜呼噜~');
    this.addAffection(3);
    this.setState('happy');
    setTimeout(() => { if (this.state === 'happy') this.setState('idle'); }, 1500);
  }

  play() {
    this.showBubble('来玩来玩！');
    this.addAffection(8);
    this.setState('happy');
    this.walkTarget = this.x + (Math.random() > 0.5 ? 200 : -200);
    setTimeout(() => { if (this.state === 'happy') this.setState('idle'); }, 2000);
  }

  update(deltaTime, canvasWidth, canvasHeight) {
    if (this.pinnedMode) {
      this.x = canvasWidth / 2;
      this.y = canvasHeight - 24;
      this.vx = 0;
      this.walkTarget = null;
    }

    this.groundY = this.pinnedMode ? canvasHeight - 24 : canvasHeight - 120 * this.scale;
    const scaledSize = FRAME_SIZE * this.scale;
    const halfW = scaledSize / 2;

    // 重力
    if (this.isFalling && !this.isDragging) {
      this.vy += GRAVITY;
      this.y += this.vy;
      if (this.y >= this.groundY) {
        this.y = this.groundY;
        this.vy = -this.vy * BOUNCE_DAMPING;
        if (Math.abs(this.vy) < 2) {
          this.vy = 0;
          this.isFalling = false;
          this.setState('idle');
        }
      }
    }

    // 边界检测
    if (this.x < halfW) { this.x = halfW; this.vx = 0; }
    if (this.x > canvasWidth - halfW) { this.x = canvasWidth - halfW; this.vx = 0; }

    // 移动
    if (!this.pinnedMode && !this.isDragging && !this.isFalling) {
      this.x += this.vx;
    }

    // 动画帧更新
    this.frameTimer += deltaTime;
    if (this.frameTimer >= this.animState.speed) {
      this.frameTimer = 0;
      this.frame = (this.frame + 1) % this.animState.frames;
    }

    // 行为 AI
    if (!this.pinnedMode && !this.isDragging && !this.isFalling) {
      this.behaviorTimer += deltaTime;
      const timeSinceInteraction = Date.now() - this.lastInteraction;

      // 长时间无操作 → 睡觉
      if (timeSinceInteraction > 60000 && this.state !== 'sleep') {
        this.setState('sleep');
        this.showBubble();
      }

      if (this.behaviorTimer >= this.behaviorInterval) {
        this.behaviorTimer = 0;
        this.behaviorInterval = 3000 + Math.random() * 8000;
        this.doRandomBehavior(timeSinceInteraction);
      }
    }

    if (this.pinnedMode && !this.isDragging && !this.isFalling) {
      this.behaviorTimer += deltaTime;
      const timeSinceInteraction = Date.now() - this.lastInteraction;
      if (timeSinceInteraction > 60000 && this.state !== 'sleep') {
        this.setState('sleep');
        this.showBubble();
      }
      if (this.behaviorTimer >= this.behaviorInterval) {
        this.behaviorTimer = 0;
        this.behaviorInterval = 3000 + Math.random() * 8000;
        const rand = Math.random();
        if (this.state === 'sleep' && rand < 0.3) {
          this.setState('idle');
          this.showBubble('醒了~');
        } else if (rand < 0.5) {
          this.setState('idle');
          this.showBubble();
        }
      }
    }

    // 行走目标
    if (!this.pinnedMode && this.walkTarget !== null && !this.isDragging && !this.isFalling) {
      const diff = this.walkTarget - this.x;
      if (Math.abs(diff) < 5) {
        this.walkTarget = null;
        this.vx = 0;
        this.setState('idle');
      } else {
        this.vx = diff > 0 ? 1.5 * this.scale : -1.5 * this.scale;
        this.direction = diff > 0 ? 1 : -1;
        if (this.state !== 'walk') this.setState('walk');
      }
    }

    // 气泡计时
    if (this.bubbleTimer > 0) {
      this.bubbleTimer--;
      this.bubbleX = this.x;
      this.bubbleY = this.y - scaledSize - 20;
    }
  }

  doRandomBehavior(timeSinceInteraction) {
    const rand = Math.random();
    if (this.state === 'sleep') {
      if (rand < 0.3) {
        this.setState('idle');
        this.showBubble('醒了~');
      }
      return;
    }
    if (rand < 0.4) {
      // 左右走走
      const target = this.x + (Math.random() - 0.5) * 300;
      this.walkTarget = Math.max(40, Math.min(this.canvas.width - 40, target));
    } else if (rand < 0.6) {
      this.setState('idle');
      this.showBubble();
    } else if (rand < 0.8 && timeSinceInteraction > 30000) {
      this.setState('sleep');
      this.showBubble();
    }
  }

  draw() {
    if (!this.spriteLoaded) {
      this.drawFallback();
      return;
    }

    const ctx = this.ctx;
    const scaledSize = FRAME_SIZE * this.scale;
    const drawX = this.x - scaledSize / 2;
    const drawY = this.y - scaledSize;

    // 确定源矩形
    const anim = this.animState;
    let col = this.frame;
    if (anim.cols) {
      col = anim.cols[this.frame] || 0;
    }
    const srcX = col * FRAME_SIZE;
    const srcY = anim.row * FRAME_SIZE;

    ctx.save();
    if (this.direction === -1) {
      ctx.translate(this.x, 0);
      ctx.scale(-1, 1);
      ctx.drawImage(
        this.spriteSheet,
        srcX, srcY, FRAME_SIZE, FRAME_SIZE,
        -scaledSize / 2, drawY, scaledSize, scaledSize
      );
    } else {
      ctx.drawImage(
        this.spriteSheet,
        srcX, srcY, FRAME_SIZE, FRAME_SIZE,
        drawX, drawY, scaledSize, scaledSize
      );
    }
    ctx.restore();

    // 绘制气泡
    if (this.bubbleTimer > 0 && this.bubbleText) {
      this.drawBubble();
    }
  }

  drawFallback() {
    const ctx = this.ctx;
    const s = FRAME_SIZE * this.scale;
    const x = this.x - s / 2;
    const y = this.y - s;

    // 简单的猫咪占位符
    ctx.fillStyle = '#ffb6c1';
    ctx.beginPath();
    ctx.ellipse(this.x, this.y - s / 2, s / 2, s / 2, 0, 0, Math.PI * 2);
    ctx.fill();

    // 耳朵
    ctx.beginPath();
    ctx.moveTo(x + s * 0.2, y + s * 0.3);
    ctx.lineTo(x + s * 0.35, y);
    ctx.lineTo(x + s * 0.5, y + s * 0.3);
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(x + s * 0.5, y + s * 0.3);
    ctx.lineTo(x + s * 0.65, y);
    ctx.lineTo(x + s * 0.8, y + s * 0.3);
    ctx.fill();

    // 眼睛
    ctx.fillStyle = '#333';
    ctx.beginPath();
    ctx.arc(this.x - s * 0.15, this.y - s * 0.55, s * 0.06, 0, Math.PI * 2);
    ctx.arc(this.x + s * 0.15, this.y - s * 0.55, s * 0.06, 0, Math.PI * 2);
    ctx.fill();

    // 嘴巴
    ctx.strokeStyle = '#333';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(this.x, this.y - s * 0.4, s * 0.08, 0, Math.PI);
    ctx.stroke();

    // 气泡
    if (this.bubbleTimer > 0 && this.bubbleText) {
      this.drawBubble();
    }
  }

  drawBubble() {
    const ctx = this.ctx;
    ctx.font = `${12 * this.scale}px sans-serif`;
    const textWidth = ctx.measureText(this.bubbleText).width;
    const padding = 10;
    const bw = textWidth + padding * 2;
    const bh = 28 * this.scale;
    const bx = this.bubbleX - bw / 2;
    const by = this.bubbleY - bh - 10;

    ctx.fillStyle = 'rgba(255,255,255,0.92)';
    ctx.strokeStyle = '#ffb6c1';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.roundRect(bx, by, bw, bh, 8);
    ctx.fill();
    ctx.stroke();

    // 小三角
    ctx.fillStyle = 'rgba(255,255,255,0.92)';
    ctx.beginPath();
    ctx.moveTo(this.bubbleX - 6, by + bh);
    ctx.lineTo(this.bubbleX, by + bh + 7);
    ctx.lineTo(this.bubbleX + 6, by + bh);
    ctx.fill();

    ctx.fillStyle = '#333';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(this.bubbleText, this.bubbleX, by + bh / 2);
  }

  hitTest(mx, my) {
    const s = FRAME_SIZE * this.scale;
    const left = this.x - s / 2;
    const top = this.y - s;
    return mx >= left && mx <= left + s && my >= top && my <= top + s;
  }
}
