-- KEYS[1]: Chave do contador (ex: "rl:login:ip:192.168.1.1")
-- ARGV[1]: Timestamp UTC atual em milissegundos
-- ARGV[2]: Janela de tempo em milissegundos (ex: 900000 para 15 min)
-- ARGV[3]: Limite máximo permitido na janela
-- ARGV[4]: Identificador único da requisição (ex: UUID)

local key = KEYS[1]
local now = tonumber(ARGV[1])
local window = tonumber(ARGV[2])
local limit = tonumber(ARGV[3])
local memberId = ARGV[4]
local clearBefore = now - window

-- 1. Remove entradas fora da janela deslizante
redis.call('ZREMRANGEBYSCORE', key, '-inf', clearBefore)

-- 2. Conta requisições ativas na janela
local currentRequests = redis.call('ZCARD', key)

if currentRequests < limit then
    -- 3. Adiciona requisição atual com score = timestamp
    redis.call('ZADD', key, now, memberId)
    -- Define TTL para expirar a chave após o fim da janela
    redis.call('PEXPIRE', key, window)
    return { 1, limit - currentRequests - 1, 0 } -- { Permitido (1), Restantes, RetryAfterSegundos }
else
    -- Limite excedido: calcula tempo para desocupar o item mais antigo
    local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
    local retryAfter = 0
    if #oldest > 0 then
        retryAfter = math.ceil((tonumber(oldest[2]) + window - now) / 1000)
        if retryAfter < 1 then retryAfter = 1 end
    end
    return { 0, 0, retryAfter } -- { Bloqueado (0), Restantes, RetryAfterSegundos }
end
