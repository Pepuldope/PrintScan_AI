-- Run this after 01_create_users.sql
-- Adds user_profiles, chats, and messages tables

CREATE TABLE IF NOT EXISTS user_profiles (
    user_id       INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    printer_name  VARCHAR(200) NOT NULL DEFAULT '',
    filament_type VARCHAR(200) NOT NULL DEFAULT '',
    slicer        VARCHAR(100) NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS chats (
    id          SERIAL PRIMARY KEY,
    user_id     INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title       VARCHAR(200) NOT NULL DEFAULT 'New Chat',
    is_pinned   BOOLEAN NOT NULL DEFAULT FALSE,
    photo_count INTEGER NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS messages (
    id          SERIAL PRIMARY KEY,
    chat_id     INTEGER NOT NULL REFERENCES chats(id) ON DELETE CASCADE,
    role        VARCHAR(20) NOT NULL,   -- 'user' or 'assistant'
    content     TEXT NOT NULL,
    photo_count INTEGER NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
