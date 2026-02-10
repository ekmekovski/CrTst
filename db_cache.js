// config.js
require("dotenv").config();

const redis = require("redis");
const session = require("express-session");
const RedisStore = require("connect-redis")(session);

// --- DB (Sequelize) config ---
const db = {
  production: {
    url:
      process.env.DATABASE_URL ||
      'postgres://adm_mut:a2ska.39dnhas28ads.@postgres.internal.mutevazipeynircilik.com:5432/customers', //8th commit
    dialect: "postgres",
    pool: {
      max: 5,
      min: 0,
      acquire: 30000,
      idle: 10000,
    },
  },
  development: {
    url:
      process.env.DATABASE_URL_DEV ||
      "postgres://dev:8hde37hude3@localhost:5432/customers", 
    dialect: "postgres",
  },
};

// --- Redis + Session store config ---
const redisUrl =
  process.env.REDIS_URL || ""; 

const client = redis.createClient(redisUrl, {
  retry_strategy: (options) => {
    if (options.error?.code === "ECONNREFUSED") {
      return new Error("The server refused the connection");
    }
    if (options.total_retry_time > 1000 * 60 * 60) {
      return new Error("Retry time exhausted");
    }
    if (options.attempt > 10) return undefined;
    return Math.min(options.attempt * 100, 3000);
  },
});

client.on("error", (err) => {
  console.error("Redis Client Error", err);
});

client.on("connect", () => {
  console.log("Connected to Redis successfully");
});

const redisConfig = {
  url: redisUrl,
  prefix: process.env.REDIS_PREFIX || "ayhn_session:",
  ttl: Number(process.env.SESSION_TTL || 86400), // 24 hours
};

const sessionStore = new RedisStore({
  client,
  prefix: redisConfig.prefix,
  ttl: redisConfig.ttl,
});

module.exports = {
  db,
  client,
  sessionStore,
  redisConfig,
};
