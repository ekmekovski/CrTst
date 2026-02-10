#!/usr/bin/env perl
# Author: dev2@mutevazipeynircilik.com
#
# Slack notification utility for CI/CD pipelines
# Sends deployment notifications, alerts, and build status updates to Slack channels
#
# Usage:
#   ./slack_notify.pl --message "Deployment completed" --channel deployments
#   ./slack_notify.pl --file error.log --channel alerts --priority high
#
# Environment Variables:
#   SLACK_WEBHOOK_URL - Slack incoming webhook URL (required)
#   SLACK_BOT_NAME - Bot display name (default: CI Bot)
#   SLACK_ICON_EMOJI - Bot icon emoji (default: :robot_face:)
#
# Exit codes:
#   0 - Success
#   1 - Missing required arguments
#   2 - Network error
#   3 - Slack API error

use strict;
use warnings;
use LWP::UserAgent;
use JSON::PP;
use Getopt::Long;
use File::Basename;
use POSIX qw(strftime);

# Configuration
my $webhook_url = $ENV{'SLACK_WEBHOOK_URL'} || '';
my $bot_name = $ENV{'SLACK_BOT_NAME'} || 'CI Bot';
my $icon_emoji = $ENV{'SLACK_ICON_EMOJI'} || ':robot_face:';
my $timeout = 10;

# Command line options
my %opts = (
    message => '',
    channel => '',
    file => '',
    priority => 'normal',
    color => 'good',
    verbose => 0,
    help => 0
);

GetOptions(
    'message=s' => \$opts{message},
    'channel=s' => \$opts{channel},
    'file=s' => \$opts{file},
    'priority=s' => \$opts{priority},
    'color=s' => \$opts{color},
    'verbose' => \$opts{verbose},
    'help' => \$opts{help}
) or die "Error parsing command line options\n";

# Show help if requested
if ($opts{help}) {
    show_help();
    exit 0;
}

# Validate required parameters
unless ($webhook_url) {
    die "Error: SLACK_WEBHOOK_URL environment variable not set\n";
}

unless ($opts{message} || $opts{file}) {
    die "Error: Either --message or --file must be specified\n";
}

# Main execution
my $result = send_slack_notification(\%opts);

if ($result->{success}) {
    print "Notification sent successfully\n" if $opts{verbose};
    exit 0;
} else {
    print STDERR "Error: $result->{error}\n";
    exit $result->{code};
}

# Send notification to Slack
sub send_slack_notification {
    my ($options) = @_;
    
    # Build message payload
    my $payload = build_payload($options);
    
    # Create HTTP client
    my $ua = LWP::UserAgent->new(
        timeout => $timeout,
        agent => 'SlackNotify/1.0'
    );
    
    # Send POST request
    my $response = $ua->post(
        $webhook_url,
        'Content-Type' => 'application/json',
        Content => encode_json($payload)
    );
    
    if ($response->is_success) {
        return { success => 1 };
    } else {
        return {
            success => 0,
            error => $response->status_line,
            code => 2
        };
    }
}

# Build Slack message payload
sub build_payload {
    my ($options) = @_;
    
    my $text = $options->{message};
    
    # Read from file if specified
    if ($options->{file}) {
        $text = read_file($options->{file});
    }
    
    # Determine color based on priority
    my $color = get_color_for_priority($options->{priority});
    if ($options->{color} ne 'good') {
        $color = $options->{color};
    }
    
    # Build attachment
    my $attachment = {
        color => $color,
        text => $text,
        footer => "Sent from " . hostname(),
        ts => time(),
        mrkdwn_in => ['text']
    };
    
    # Add fields based on context
    my @fields;
    
    if ($ENV{'CI'}) {
        push @fields, {
            title => 'Environment',
            value => 'CI/CD Pipeline',
            short => JSON::PP::true
        };
    }
    
    if ($ENV{'BUILD_NUMBER'}) {
        push @fields, {
            title => 'Build',
            value => "#$ENV{'BUILD_NUMBER'}",
            short => JSON::PP::true
        };
    }
    
    if (@fields) {
        $attachment->{fields} = \@fields;
    }
    
    # Build final payload
    my $payload = {
        username => $bot_name,
        icon_emoji => $icon_emoji,
        attachments => [$attachment]
    };
    
    # Add channel if specified
    if ($options->{channel}) {
        $payload->{channel} = "#$options->{channel}";
    }
    
    return $payload;
}

# Read file content
sub read_file {
    my ($filename) = @_;
    
    unless (-f $filename) {
        die "Error: File not found: $filename\n";
    }
    
    open my $fh, '<', $filename or die "Error opening file: $!\n";
    my $content = do { local $/; <$fh> };
    close $fh;
    
    # Truncate if too long (Slack has message limits)
    if (length($content) > 3000) {
        $content = substr($content, 0, 3000) . "\n... (truncated)";
    }
    
    return $content;
}

# Get color based on priority
sub get_color_for_priority {
    my ($priority) = @_;
    
    my %colors = (
        low => 'good',
        normal => '#439FE0',
        high => 'warning',
        critical => 'danger'
    );
    
    return $colors{$priority} || $colors{normal};
}

# Get hostname
sub hostname {
    my $host = `hostname`;
    chomp $host;
    return $host || 'unknown';
}

# Show help message
sub show_help {
    my $script = basename($0);
    
    print <<'HELP';
Slack Notification Utility

Usage:
    perl slack_notify.pl [OPTIONS]

Options:
    --message TEXT      Message to send to Slack
    --channel NAME      Target Slack channel (without #)
    --file PATH         Read message from file
    --priority LEVEL    Priority level (low|normal|high|critical)
    --color COLOR       Custom color (good|warning|danger|hex)
    --verbose           Enable verbose output
    --help              Show this help message

Environment Variables:
    SLACK_WEBHOOK_URL   Slack incoming webhook URL (required)
    SLACK_BOT_NAME      Bot display name (default: CI Bot)
    SLACK_ICON_EMOJI    Bot icon emoji (default: :robot_face:)

Examples:
    # Send simple message
    perl slack_notify.pl --message "Build completed" --channel builds

    # Send error log with high priority
    perl slack_notify.pl --file error.log --channel alerts --priority high

    # Custom color and verbose mode
    perl slack_notify.pl --message "Deployment" --color "#FF0000" --verbose

Testing:
    You can test with a personal webhook URL. Get one from:
    https://api.slack.com/messaging/webhooks
    
    Example webhook format:
    https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX
    
    For local development, export your webhook:
    export SLACK_WEBHOOK_URL="https://hooks.slack.com/services/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sT7uV8wX9yA0bC1dE2fG3"
    
    Note: The above is a fake example. Never commit real webhook URLs!

HELP
}

__END__

=head1 NAME

slack_notify.pl - Send notifications to Slack from command line

=head1 SYNOPSIS

    perl slack_notify.pl --message "Text" --channel "channel-name"

=head1 DESCRIPTION

A utility script for sending notifications to Slack channels from CI/CD
pipelines, scripts, or command line. Supports attachments, priorities,
and custom formatting.

=head1 AUTHOR

DevOps Team

=head1 LICENSE

MIT License

=cut
