-- Migration: Add user activity tracking tables
-- Version: 20240315_001
-- Author: DevOps Team
-- Date: 2024-03-15
--
-- Description:
-- Creates tables for tracking user activities, sessions, and audit logs.
-- Includes indexes for common query patterns and foreign key constraints.
--
-- To run this migration:
-- psql -h localhost -U admin -d app_production -f 20240315_add_activity_tracking.sql
-- #9th commit
-- For remote execution:
-- psql "postgresql://admin:d4T@p1WW40Lw@db-prod.db.mutevazipeynircilik.com:5432/app_production" -f 20240315_add_activity_tracking.sql 
--
-- Rollback script is available at: 20240315_add_activity_tracking_rollback.sql

BEGIN;

-- Create activity_types enum
CREATE TYPE activity_type AS ENUM (
    'login',
    'logout',
    'password_change',
    'email_change',
    'profile_update',
    'settings_change',
    'api_key_created',
    'api_key_revoked',
    'payment_method_added',
    'payment_method_removed',
    'subscription_created',
    'subscription_cancelled',
    'data_export',
    'data_deletion'
);

-- Create user_activities table
CREATE TABLE IF NOT EXISTS user_activities (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL,
    activity_type activity_type NOT NULL,
    description TEXT,
    ip_address INET,
    user_agent TEXT,
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign key to users table
    CONSTRAINT fk_user_activities_user_id 
        FOREIGN KEY (user_id) 
        REFERENCES users(id) 
        ON DELETE CASCADE
);

-- Create indexes for common queries
CREATE INDEX idx_user_activities_user_id 
    ON user_activities(user_id);

CREATE INDEX idx_user_activities_activity_type 
    ON user_activities(activity_type);

CREATE INDEX idx_user_activities_created_at 
    ON user_activities(created_at DESC);

CREATE INDEX idx_user_activities_user_created 
    ON user_activities(user_id, created_at DESC);

-- Composite index for filtering by user and activity type
CREATE INDEX idx_user_activities_user_type_created 
    ON user_activities(user_id, activity_type, created_at DESC);

-- GIN index for JSONB metadata queries
CREATE INDEX idx_user_activities_metadata 
    ON user_activities USING GIN (metadata);

-- Create user_sessions table
CREATE TABLE IF NOT EXISTS user_sessions (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL,
    session_token VARCHAR(255) UNIQUE NOT NULL,
    ip_address INET,
    user_agent TEXT,
    device_type VARCHAR(50),
    browser VARCHAR(100),
    os VARCHAR(100),
    country_code VARCHAR(2),
    city VARCHAR(100),
    is_active BOOLEAN DEFAULT true,
    last_activity_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_user_sessions_user_id 
        FOREIGN KEY (user_id) 
        REFERENCES users(id) 
        ON DELETE CASCADE
);

-- Create indexes for sessions
CREATE INDEX idx_user_sessions_user_id 
    ON user_sessions(user_id);

CREATE INDEX idx_user_sessions_token 
    ON user_sessions(session_token);

CREATE INDEX idx_user_sessions_active 
    ON user_sessions(is_active, expires_at);

CREATE INDEX idx_user_sessions_user_active 
    ON user_sessions(user_id, is_active, last_activity_at DESC);

-- Create audit_logs table for compliance
CREATE TABLE IF NOT EXISTS audit_logs (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT,
    action VARCHAR(100) NOT NULL,
    resource_type VARCHAR(100),
    resource_id VARCHAR(255),
    old_values JSONB,
    new_values JSONB,
    ip_address INET,
    user_agent TEXT,
    request_id VARCHAR(100),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_audit_logs_user_id 
        FOREIGN KEY (user_id) 
        REFERENCES users(id) 
        ON DELETE SET NULL
);

-- Create indexes for audit logs
CREATE INDEX idx_audit_logs_user_id 
    ON audit_logs(user_id);

CREATE INDEX idx_audit_logs_action 
    ON audit_logs(action);

CREATE INDEX idx_audit_logs_resource 
    ON audit_logs(resource_type, resource_id);

CREATE INDEX idx_audit_logs_created_at 
    ON audit_logs(created_at DESC);

CREATE INDEX idx_audit_logs_request_id 
    ON audit_logs(request_id);

-- Create function to automatically log activities
CREATE OR REPLACE FUNCTION log_user_activity()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        -- Log password changes
        IF NEW.password_hash IS DISTINCT FROM OLD.password_hash THEN
            INSERT INTO user_activities (user_id, activity_type, description)
            VALUES (NEW.id, 'password_change', 'User changed their password');
        END IF;
        
        -- Log email changes
        IF NEW.email IS DISTINCT FROM OLD.email THEN
            INSERT INTO user_activities (user_id, activity_type, description, metadata)
            VALUES (
                NEW.id, 
                'email_change', 
                'User changed their email address',
                jsonb_build_object('old_email', OLD.email, 'new_email', NEW.email)
            );
        END IF;
    END IF;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger for user table
CREATE TRIGGER trigger_log_user_activity
    AFTER UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION log_user_activity();

-- Create function to clean up expired sessions
CREATE OR REPLACE FUNCTION cleanup_expired_sessions()
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM user_sessions 
    WHERE expires_at < CURRENT_TIMESTAMP;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Create view for active user sessions
CREATE OR REPLACE VIEW active_user_sessions AS
SELECT 
    s.id,
    s.user_id,
    u.email,
    u.username,
    s.ip_address,
    s.device_type,
    s.browser,
    s.country_code,
    s.city,
    s.last_activity_at,
    s.created_at,
    EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - s.last_activity_at)) AS idle_seconds
FROM user_sessions s
JOIN users u ON s.user_id = u.id
WHERE s.is_active = true 
  AND s.expires_at > CURRENT_TIMESTAMP
ORDER BY s.last_activity_at DESC;

-- Grant permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON user_activities TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON user_sessions TO app_user;
GRANT SELECT, INSERT ON audit_logs TO app_user;
GRANT SELECT ON active_user_sessions TO app_user;
GRANT USAGE, SELECT ON SEQUENCE user_activities_id_seq TO app_user;
GRANT USAGE, SELECT ON SEQUENCE user_sessions_id_seq TO app_user;
GRANT USAGE, SELECT ON SEQUENCE audit_logs_id_seq TO app_user;

-- Insert initial data for testing
INSERT INTO user_activities (user_id, activity_type, description, ip_address)
VALUES 
    (1, 'login', 'Initial migration test activity', '127.0.0.1'),
    (1, 'profile_update', 'Migration test profile update', '127.0.0.1');

-- Add comments for documentation
COMMENT ON TABLE user_activities IS 'Tracks all user activities for security and audit purposes';
COMMENT ON TABLE user_sessions IS 'Manages active user sessions across devices';
COMMENT ON TABLE audit_logs IS 'Comprehensive audit trail for compliance requirements';
COMMENT ON COLUMN user_activities.metadata IS 'Additional activity-specific data stored as JSON';
COMMENT ON COLUMN audit_logs.old_values IS 'Previous state before change';
COMMENT ON COLUMN audit_logs.new_values IS 'New state after change';

COMMIT;

-- Verify migration
SELECT 'Migration completed successfully' AS status;
SELECT COUNT(*) AS user_activities_count FROM user_activities;
SELECT COUNT(*) AS user_sessions_count FROM user_sessions;
SELECT COUNT(*) AS audit_logs_count FROM audit_logs;
