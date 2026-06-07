import { Component, type ErrorInfo, type ReactNode } from 'react';
import { toast } from 'sonner';

interface Props {
    children: ReactNode;
    fallback?: ReactNode;
}

interface State {
    hasError: boolean;
    error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
    constructor(props: Props) {
        super(props);
        this.state = { hasError: false, error: null };
    }

    static getDerivedStateFromError(error: Error): State {
        return { hasError: true, error };
    }

    componentDidCatch(error: Error, info: ErrorInfo): void {
        console.error('[ErrorBoundary]', error, info);
        toast.error("Une erreur inattendue s'est produite.", { duration: 6000 });
    }

    render(): ReactNode {
        if (this.state.hasError) {
            return (
                this.props.fallback ?? (
                    <div className="p-8 text-center">
                        <h2 className="text-lg font-semibold">Une erreur est survenue</h2>
                        <p className="text-sm text-muted-foreground">
                            {this.state.error?.message ?? 'Erreur inattendue'}
                        </p>
                        <button
                            className="mt-4 underline text-sm"
                            onClick={() => this.setState({ hasError: false, error: null })}
                        >
                            Réessayer
                        </button>
                    </div>
                )
            );
        }
        return this.props.children;
    }
}
